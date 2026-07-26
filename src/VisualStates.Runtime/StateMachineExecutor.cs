using VisualStates.Core;
using VisualStates.Core.Models;

namespace VisualStates.Runtime;

/// <summary>
/// Host-supplied context used by generated and interpreted state machines to
/// raise events, invoke methods, and resolve services.
/// </summary>
public interface IStateMachineContext
{
    /// <summary>Raises a named event asynchronously.</summary>
    /// <param name="eventName">Event identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseEventAsync(string eventName, CancellationToken cancellationToken = default);

    /// <summary>Invokes a named host method asynchronously.</summary>
    /// <param name="methodName">Method identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvokeMethodAsync(string methodName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a host service of type <typeparamref name="T"/>, or returns
    /// <see langword="null"/> when unavailable.
    /// </summary>
    /// <typeparam name="T">Service type.</typeparam>
    T? GetService<T>() where T : class;
}

/// <summary>
/// Contract implemented by code generated from a <see cref="StateProject"/>.
/// </summary>
public interface IGeneratedStateMachine
{
    /// <summary>
    /// Runs the generated state machine against <paramref name="context"/>.
    /// </summary>
    /// <param name="context">Host context for events and method calls.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RunAsync(IStateMachineContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// A planned execution position: a box id and optional step id.
/// </summary>
/// <param name="BoxId">Owning state box id.</param>
/// <param name="StepId">Step id within the box, or null for an empty box.</param>
public readonly record struct ExecutionPlanItem(string BoxId, string? StepId);

/// <summary>
/// Interprets a <see cref="StateProject"/> at runtime without emitting C# source:
/// plans a topological execution order, runs each step, and diverts to error
/// handlers when a step throws.
/// </summary>
public sealed class StateMachineExecutor
{
    /// <summary>
    /// Returns the topological happy-path execution order for <paramref name="project"/>
    /// as box/step id pairs suitable for UI preview and stepping.
    /// </summary>
    /// <param name="project">Project graph to plan.</param>
    public IReadOnlyList<ExecutionPlanItem> GetExecutionPlan(StateProject project)
    {
        var order = ExecutionPlanner.Plan(project);
        var items = new List<ExecutionPlanItem>(order.Count);
        foreach (var step in order)
            items.Add(new ExecutionPlanItem(step.Box.Id, step.Step?.Id));
        return items;
    }

    /// <summary>
    /// Executes <paramref name="project"/> against <paramref name="context"/>,
    /// stopping after the first handled error branch.
    /// </summary>
    /// <param name="project">Project graph to interpret.</param>
    /// <param name="context">Host context for events and method calls.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExecuteAsync(
        StateProject project,
        IStateMachineContext context,
        CancellationToken cancellationToken = default)
    {
        var order = ExecutionPlanner.Plan(project);
        var errorHandlers = ExecutionPlanner.BuildErrorHandlers(project);

        foreach (var step in order)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await step.ExecuteAsync(context, cancellationToken);
            }
            catch (Exception) when (errorHandlers.TryGetValue(step.Key, out var handler))
            {
                await handler.ExecuteAsync(context, cancellationToken);
                return;
            }
        }
    }
}

/// <summary>
/// Builds topological execution plans and error-handler maps for a project.
/// </summary>
internal static class ExecutionPlanner
{
    /// <summary>
    /// Returns steps in a topological order derived from happy-path connections
    /// and zone visual chaining.
    /// </summary>
    /// <param name="project">Project to plan.</param>
    public static IReadOnlyList<RuntimeStep> Plan(StateProject project)
    {
        var steps = new List<RuntimeStep>();
        foreach (var box in project.Boxes)
        {
            if (box.Steps.Count == 0)
            {
                steps.Add(new RuntimeStep(box, null));
                continue;
            }

            steps.AddRange(box.Steps.Select(step => new RuntimeStep(box, step)));
        }

        if (steps.Count == 0)
            return steps;

        var stepMap = steps.ToDictionary(s => s.Key, s => s);
        var incoming = steps.ToDictionary(s => s.Key, _ => 0);
        var adjacency = steps.ToDictionary(s => s.Key, _ => new List<RuntimeStepKey>());

        foreach (var connection in project.Connections)
        {
            if (connection.IsError || connection.SourceSide == PinSide.Error)
                continue;

            var source = ResolveConnectionSource(project, connection);
            var target = ResolveConnectionTarget(project, connection);
            if (source is null || target is null
                || !stepMap.ContainsKey(source.Value) || !stepMap.ContainsKey(target.Value)
                || source.Value.Equals(target.Value))
                continue;

            adjacency[source.Value].Add(target.Value);
            incoming[target.Value]++;
        }

        foreach (var zone in project.Zones)
        {
            var children = ZoneFlow.GetOrderedChildren(project, zone.Id);
            foreach (var child in children)
            {
                for (var s = 0; s < child.Steps.Count - 1; s++)
                {
                    var from = new RuntimeStepKey(child.Id, child.Steps[s].Id);
                    var to = new RuntimeStepKey(child.Id, child.Steps[s + 1].Id);
                    if (!stepMap.ContainsKey(from) || !stepMap.ContainsKey(to) || from.Equals(to))
                        continue;

                    adjacency[from].Add(to);
                    incoming[to]++;
                }
            }

            for (var i = 0; i < children.Count - 1; i++)
            {
                var from = BoxExitKey(children[i]);
                var to = BoxEnterKey(children[i + 1]);
                if (!stepMap.ContainsKey(from) || !stepMap.ContainsKey(to) || from.Equals(to))
                    continue;

                adjacency[from].Add(to);
                incoming[to]++;
            }
        }

        // Error-pin targets are not part of the happy path. They used to appear as
        // zero-indegree seeds (error edges are ignored when building adjacency) and
        // ran before/alongside the real entry. Start from the entry only, and never
        // append unvisited error-only targets onto the main plan.
        var errorTargets = CollectErrorTargets(project, stepMap);

        var queue = new Queue<RuntimeStepKey>();
        var entry = project.Boxes.FirstOrDefault(b => b.IsEntry) ?? project.Boxes.FirstOrDefault();
        if (entry is not null)
            queue.Enqueue(BoxEnterKey(entry));
        else
            queue.Enqueue(steps[0].Key);

        var ordered = new List<RuntimeStep>();
        var visited = new HashSet<RuntimeStepKey>();
        while (queue.Count > 0)
        {
            var key = queue.Dequeue();
            if (!visited.Add(key) || !stepMap.TryGetValue(key, out var runtimeStep))
                continue;

            ordered.Add(runtimeStep);
            foreach (var next in adjacency[key])
            {
                incoming[next]--;
                if (incoming[next] == 0)
                    queue.Enqueue(next);
            }
        }

        foreach (var step in steps)
        {
            if (visited.Contains(step.Key) || errorTargets.Contains(step.Key))
                continue;

            ordered.Add(step);
        }

        return ordered;
    }

    /// <summary>
    /// Collects step keys that are targets of error-pin connections.
    /// </summary>
    private static HashSet<RuntimeStepKey> CollectErrorTargets(
        StateProject project,
        IReadOnlyDictionary<RuntimeStepKey, RuntimeStep> stepMap)
    {
        var targets = new HashSet<RuntimeStepKey>();
        foreach (var connection in project.Connections)
        {
            if (!IsErrorConnection(connection))
                continue;

            var target = ResolveConnectionTarget(project, connection);
            if (target is not null && stepMap.ContainsKey(target.Value))
                targets.Add(target.Value);
        }

        return targets;
    }

    /// <summary>
    /// Maps each step key to the runtime step that should run when that step throws,
    /// based on error-pin connections (zone-level, then step-level, then box-level).
    /// </summary>
    /// <param name="project">Project whose error pins to inspect.</param>
    public static IReadOnlyDictionary<RuntimeStepKey, RuntimeStep> BuildErrorHandlers(StateProject project)
    {
        var allSteps = new Dictionary<RuntimeStepKey, RuntimeStep>();
        foreach (var box in project.Boxes)
        {
            if (box.Steps.Count == 0)
            {
                var empty = new RuntimeStep(box, null);
                allSteps[empty.Key] = empty;
                continue;
            }

            foreach (var step in box.Steps)
            {
                var runtime = new RuntimeStep(box, step);
                allSteps[runtime.Key] = runtime;
            }
        }

        var handlers = new Dictionary<RuntimeStepKey, RuntimeStep>();
        foreach (var box in project.Boxes)
        {
            if (box.Steps.Count == 0)
            {
                var key = new RuntimeStepKey(box.Id, null);
                if (TryResolveErrorHandler(project, box, step: null, allSteps, out var handler))
                    handlers[key] = handler;
                continue;
            }

            foreach (var step in box.Steps)
            {
                var key = new RuntimeStepKey(box.Id, step.Id);
                if (TryResolveErrorHandler(project, box, step, allSteps, out var handler))
                    handlers[key] = handler;
            }
        }

        return handlers;
    }

    private static bool TryResolveErrorHandler(
        StateProject project,
        StateBox box,
        StateStep? step,
        IReadOnlyDictionary<RuntimeStepKey, RuntimeStep> allSteps,
        out RuntimeStep handler)
    {
        handler = null!;

        // Inside a zone: only the zone's shared error pin applies.
        if (!string.IsNullOrWhiteSpace(box.ZoneId))
        {
            foreach (var connection in project.Connections)
            {
                if (!IsErrorConnection(connection))
                    continue;
                if (connection.SourceZoneId != box.ZoneId)
                    continue;

                var target = ResolveConnectionTarget(project, connection);
                return target is not null && allSteps.TryGetValue(target.Value, out handler!);
            }

            return false;
        }

        // Outside a zone: step-specific, then box-level.
        StateConnection? boxFallback = null;
        foreach (var connection in project.Connections)
        {
            if (!IsErrorConnection(connection))
                continue;
            if (!string.IsNullOrWhiteSpace(connection.SourceZoneId))
                continue;
            if (connection.SourceBoxId != box.Id)
                continue;

            if (step is not null && connection.SourceStepId == step.Id)
            {
                var target = ResolveConnectionTarget(project, connection);
                if (target is not null && allSteps.TryGetValue(target.Value, out handler!))
                    return true;
            }

            if (string.IsNullOrWhiteSpace(connection.SourceStepId))
                boxFallback ??= connection;
        }

        if (boxFallback is null)
            return false;

        var fallbackTarget = ResolveConnectionTarget(project, boxFallback);
        return fallbackTarget is not null && allSteps.TryGetValue(fallbackTarget.Value, out handler!);
    }

    private static bool IsErrorConnection(StateConnection connection) =>
        connection.IsError || connection.SourceSide == PinSide.Error;

    private static RuntimeStepKey? ResolveConnectionSource(StateProject project, StateConnection connection)
    {
        if (!string.IsNullOrWhiteSpace(connection.SourceZoneId))
        {
            var exit = ZoneFlow.ResolveExit(project, connection.SourceZoneId);
            return exit is null ? null : new RuntimeStepKey(exit.Value.BoxId, exit.Value.StepId);
        }

        return RuntimeStepKey.From(project, connection.SourceBoxId, connection.SourceStepId);
    }

    private static RuntimeStepKey? ResolveConnectionTarget(StateProject project, StateConnection connection)
    {
        if (!string.IsNullOrWhiteSpace(connection.TargetZoneId))
        {
            var enter = ZoneFlow.ResolveEnter(project, connection.TargetZoneId);
            return enter is null ? null : new RuntimeStepKey(enter.Value.BoxId, enter.Value.StepId);
        }

        return RuntimeStepKey.From(project, connection.TargetBoxId, connection.TargetStepId);
    }

    private static RuntimeStepKey BoxEnterKey(StateBox box) =>
        box.Steps.Count > 0
            ? new RuntimeStepKey(box.Id, box.Steps[0].Id)
            : new RuntimeStepKey(box.Id, null);

    private static RuntimeStepKey BoxExitKey(StateBox box) =>
        box.Steps.Count > 0
            ? new RuntimeStepKey(box.Id, box.Steps[^1].Id)
            : new RuntimeStepKey(box.Id, null);
}

/// <summary>
/// Identity of a runtime step: a box id plus an optional step id.
/// </summary>
/// <param name="BoxId">Owning box id.</param>
/// <param name="StepId">Step id, or null for an empty box.</param>
internal readonly record struct RuntimeStepKey(string BoxId, string? StepId)
{
    /// <summary>
    /// Builds a key for <paramref name="boxId"/>/<paramref name="stepId"/>,
    /// falling back to the box's last step when the step id is missing or unknown.
    /// </summary>
    /// <param name="project">Project used to resolve the box.</param>
    /// <param name="boxId">Box id.</param>
    /// <param name="stepId">Optional step id.</param>
    public static RuntimeStepKey From(StateProject project, string boxId, string? stepId)
    {
        var box = project.FindBox(boxId);
        if (box is null)
            return new RuntimeStepKey(boxId, stepId);

        if (!string.IsNullOrWhiteSpace(stepId) && box.Steps.Any(s => s.Id == stepId))
            return new RuntimeStepKey(box.Id, stepId);

        if (box.Steps.Count > 0)
            return new RuntimeStepKey(box.Id, box.Steps[^1].Id);

        return new RuntimeStepKey(box.Id, null);
    }
}

/// <summary>
/// Executable wrapper around a <see cref="StateBox"/> and optional <see cref="StateStep"/>.
/// </summary>
internal sealed class RuntimeStep
{
    /// <summary>
    /// Creates a runtime step for <paramref name="box"/> and optional
    /// <paramref name="step"/>.
    /// </summary>
    /// <param name="box">Owning box.</param>
    /// <param name="step">Step to run, or null for a no-op empty box.</param>
    public RuntimeStep(StateBox box, StateStep? step)
    {
        Box = box;
        Step = step;
        Key = new RuntimeStepKey(box.Id, step?.Id);
    }

    /// <summary>Owning state box.</summary>
    public StateBox Box { get; }

    /// <summary>Step to execute, or null for an empty box.</summary>
    public StateStep? Step { get; }

    /// <summary>Stable key used for planning and error-handler lookup.</summary>
    public RuntimeStepKey Key { get; }

    /// <summary>
    /// Executes this step against <paramref name="context"/> according to
    /// <see cref="StateStep.Kind"/>.
    /// </summary>
    /// <param name="context">Host context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExecuteAsync(IStateMachineContext context, CancellationToken cancellationToken)
    {
        if (Step is null)
        {
            await Task.CompletedTask;
            return;
        }

        switch (Step.Kind)
        {
            case StepKind.SetVariable:
                await Task.CompletedTask;
                break;
            case StepKind.CallEvent:
                await context.RaiseEventAsync(Step.EventName ?? Step.TargetName ?? "OnEvent", cancellationToken);
                break;
            case StepKind.CallMethod:
                await context.InvokeMethodAsync(Step.MethodName ?? Step.TargetName ?? "Execute", cancellationToken);
                break;
        }
    }
}

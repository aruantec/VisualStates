using VisualStates.Core;
using VisualStates.Core.Models;

namespace VisualStates.Runtime;

public interface IStateMachineContext
{
    Task RaiseEventAsync(string eventName, CancellationToken cancellationToken = default);
    Task InvokeMethodAsync(string methodName, CancellationToken cancellationToken = default);
    T? GetService<T>() where T : class;
}

public interface IGeneratedStateMachine
{
    Task RunAsync(IStateMachineContext context, CancellationToken cancellationToken = default);
}

public sealed class StateMachineExecutor
{
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

internal static class ExecutionPlanner
{
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

        var queue = new Queue<RuntimeStepKey>(
            incoming.Where(kv => kv.Value == 0).Select(kv => kv.Key));

        if (queue.Count == 0)
        {
            var entry = project.Boxes.FirstOrDefault(b => b.IsEntry) ?? project.Boxes.FirstOrDefault();
            if (entry is not null)
                queue.Enqueue(RuntimeStepKey.From(project, entry.Id, entry.Steps.FirstOrDefault()?.Id));
            else
                queue.Enqueue(steps[0].Key);
        }

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
            if (!visited.Contains(step.Key))
                ordered.Add(step);
        }

        return ordered;
    }

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

internal readonly record struct RuntimeStepKey(string BoxId, string? StepId)
{
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

internal sealed class RuntimeStep
{
    public RuntimeStep(StateBox box, StateStep? step)
    {
        Box = box;
        Step = step;
        Key = new RuntimeStepKey(box.Id, step?.Id);
    }

    public StateBox Box { get; }
    public StateStep? Step { get; }
    public RuntimeStepKey Key { get; }

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

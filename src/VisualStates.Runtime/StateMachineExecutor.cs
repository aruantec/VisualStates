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
        foreach (var step in order)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await step.ExecuteAsync(context, cancellationToken);
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
            var source = RuntimeStepKey.From(project, connection.SourceBoxId, connection.SourceStepId);
            var target = RuntimeStepKey.From(project, connection.TargetBoxId, connection.TargetStepId);
            if (!stepMap.ContainsKey(source) || !stepMap.ContainsKey(target) || source.Equals(target))
                continue;

            adjacency[source].Add(target);
            incoming[target]++;
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

namespace VisualStates.Core.Models;

public sealed class StateStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Step";
    public StepKind Kind { get; set; } = StepKind.SetVariable;
    public string? TargetName { get; set; }
    public string? Expression { get; set; }
    public string? MethodName { get; set; }
    public string? EventName { get; set; }
    public string? Arguments { get; set; }
}

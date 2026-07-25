namespace VisualStates.Core.Models;

public sealed class StateConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceBoxId { get; set; } = string.Empty;
    public string? SourceStepId { get; set; }
    public string TargetBoxId { get; set; } = string.Empty;
    public string? TargetStepId { get; set; }
}

namespace VisualStates.Core.Models;

public sealed class StateConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceBoxId { get; set; } = string.Empty;
    public string? SourceStepId { get; set; }
    public string? SourceZoneId { get; set; }
    public PinSide SourceSide { get; set; } = PinSide.Right;
    public string TargetBoxId { get; set; } = string.Empty;
    public string? TargetStepId { get; set; }
    public string? TargetZoneId { get; set; }
    public PinSide TargetSide { get; set; } = PinSide.Left;

    /// <summary>
    /// When true, this wire is an error/exit branch (from an Error pin),
    /// not part of the happy-path execution order.
    /// </summary>
    public bool IsError { get; set; }
}

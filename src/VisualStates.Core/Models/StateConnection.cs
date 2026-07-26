namespace VisualStates.Core.Models;

/// <summary>
/// A directed wire between two pins on the graph.
/// Source and target are identified by box/step and/or zone ids; the pin side
/// records which geometric pin the wire attaches to. Direction is defined by
/// the drag that created the connection (source = start pin, target = drop pin),
/// not by the side of either pin.
/// </summary>
public sealed class StateConnection
{
    /// <summary>Unique identifier for this connection.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Id of the source <see cref="StateBox"/>. Empty when the source is a zone
    /// with no child boxes.
    /// </summary>
    public string SourceBoxId { get; set; } = string.Empty;

    /// <summary>
    /// Optional id of the source step within the source box.
    /// Null means the connection attaches to the box itself.
    /// </summary>
    public string? SourceStepId { get; set; }

    /// <summary>
    /// When set, the source endpoint is a zone pin; the connection leaves the
    /// zone via its exit box.
    /// </summary>
    public string? SourceZoneId { get; set; }

    /// <summary>Geometric side of the source pin.</summary>
    public PinSide SourceSide { get; set; } = PinSide.Right;

    /// <summary>
    /// Id of the target <see cref="StateBox"/>. Empty when the target is a zone
    /// with no child boxes.
    /// </summary>
    public string TargetBoxId { get; set; } = string.Empty;

    /// <summary>
    /// Optional id of the target step within the target box.
    /// Null means the connection attaches to the box itself.
    /// </summary>
    public string? TargetStepId { get; set; }

    /// <summary>
    /// When set, the target endpoint is a zone pin; the connection enters the
    /// zone via its enter box.
    /// </summary>
    public string? TargetZoneId { get; set; }

    /// <summary>Geometric side of the target pin.</summary>
    public PinSide TargetSide { get; set; } = PinSide.Left;

    /// <summary>
    /// When <see langword="true"/>, this wire is an error/exit branch (typically
    /// from an <see cref="PinSide.Error"/> pin) and is excluded from happy-path
    /// execution order.
    /// </summary>
    public bool IsError { get; set; }
}

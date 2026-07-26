namespace VisualStates.Core.Models;

/// <summary>
/// A state node on the graph: a rectangular box that holds an ordered list of steps.
/// </summary>
public sealed class StateBox
{
    /// <summary>Unique identifier for this box.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Display name shown in the box header.</summary>
    public string Name { get; set; } = "State";

    /// <summary>Graph-space X coordinate of the box's top-left corner.</summary>
    public double X { get; set; }

    /// <summary>Graph-space Y coordinate of the box's top-left corner.</summary>
    public double Y { get; set; }

    /// <summary>Width of the box in graph units.</summary>
    public double Width { get; set; } = 220;

    /// <summary>
    /// When <see langword="true"/>, this box is the preferred entry point for
    /// execution ordering when no other root can be determined.
    /// </summary>
    public bool IsEntry { get; set; }

    /// <summary>
    /// Optional hex color (e.g. <c>#E74C3C</c>) for the box header.
    /// When null, a palette color is derived from <see cref="Id"/>.
    /// </summary>
    public string? HeaderColor { get; set; }

    /// <summary>
    /// Id of the parent <see cref="Zone"/> when this box is nested inside a zone;
    /// <see langword="null"/> when the box sits freely on the canvas.
    /// </summary>
    public string? ZoneId { get; set; }

    /// <summary>Ordered list of steps executed while this state is active.</summary>
    public List<StateStep> Steps { get; set; } = [];
}

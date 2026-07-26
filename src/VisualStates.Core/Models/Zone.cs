namespace VisualStates.Core.Models;

/// <summary>
/// A visual container that groups related <see cref="StateBox"/> nodes.
/// Children reference the zone via <see cref="StateBox.ZoneId"/>.
/// Zone enter/exit flow follows visual reading order (top-to-bottom, then left-to-right).
/// </summary>
public sealed class Zone
{
    /// <summary>Unique identifier for this zone.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Display name shown in the zone title bar.</summary>
    public string Name { get; set; } = "Zone";

    /// <summary>Graph-space X coordinate of the zone's top-left corner (title bar).</summary>
    public double X { get; set; }

    /// <summary>Graph-space Y coordinate of the zone's top-left corner (title bar).</summary>
    public double Y { get; set; }

    /// <summary>Width of the zone body in graph units.</summary>
    public double Width { get; set; } = 360;

    /// <summary>Height of the zone body in graph units (excludes the title bar).</summary>
    public double Height { get; set; } = 280;

    /// <summary>
    /// Optional hex color (e.g. <c>#3498DB</c>) for the zone border and accent.
    /// When null, a palette color is derived from <see cref="Id"/>.
    /// </summary>
    public string? BorderColor { get; set; }
}

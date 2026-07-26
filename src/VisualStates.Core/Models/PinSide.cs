namespace VisualStates.Core.Models;

/// <summary>
/// Geometric side of a connection pin on a box, step, or zone.
/// Pins are direction-agnostic: the user chooses source and target by drag gesture,
/// not by which side the pin sits on.
/// </summary>
public enum PinSide
{
    /// <summary>Pin centered on the left edge.</summary>
    Left,

    /// <summary>Pin centered on the right edge.</summary>
    Right,

    /// <summary>Pin centered on the top edge.</summary>
    Top,

    /// <summary>Pin centered on the bottom edge.</summary>
    Bottom,

    /// <summary>
    /// Dedicated error / exit pin (top-right corner). Connections from this pin
    /// are treated as error branches rather than happy-path flow.
    /// </summary>
    Error
}

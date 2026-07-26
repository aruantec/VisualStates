namespace VisualStates.ViewModels;

/// <summary>
/// Layout constants for zone chrome (title bar, body, resize handles).
/// </summary>
public static class ZoneLayout
{
    /// <summary>Height of the zone title bar in graph units.</summary>
    public const double TitleHeight = 26;

    /// <summary>Gap between the title bar and the dashed body rectangle.</summary>
    public const double TitleGap = 6;

    /// <summary>Minimum zone body width.</summary>
    public const double MinWidth = 180;

    /// <summary>Minimum zone body height.</summary>
    public const double MinHeight = 140;

    /// <summary>Size of a corner resize handle in graph units.</summary>
    public const double HandleSize = 8;

    /// <summary>Preferred on-screen size of a corner handle (scaled by zoom).</summary>
    public const double CornerHandleScreen = 12;

    /// <summary>Corner radius of the zone body rectangle.</summary>
    public const double BodyCornerRadius = 10;
}

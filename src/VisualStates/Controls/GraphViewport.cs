using Avalonia;

namespace VisualStates.Controls;

internal static class GraphViewport
{
    public const double MinZoom = 0.25;
    public const double MaxZoom = 3.0;

    /// <summary>Higher = snappier chase toward the target zoom (iOS-like ease-out).</summary>
    public const double ZoomSmoothing = 14.0;

    public static Point ScreenToGraph(Point screen, double panX, double panY, double zoom) =>
        new((screen.X - panX) / zoom, (screen.Y - panY) / zoom);

    public static Point GraphToScreen(Point graph, double panX, double panY, double zoom) =>
        new(graph.X * zoom + panX, graph.Y * zoom + panY);

    public static (double PanX, double PanY, double Zoom) ZoomAt(
        Point screenPoint,
        double panX,
        double panY,
        double zoom,
        double factor)
    {
        var newZoom = Math.Clamp(zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - zoom) < 1e-6)
            return (panX, panY, zoom);

        var graph = ScreenToGraph(screenPoint, panX, panY, zoom);
        return ZoomTo(screenPoint, graph, panX, panY, newZoom);
    }

    /// <summary>
    /// Sets zoom while keeping a fixed graph point under a fixed screen point.
    /// </summary>
    public static (double PanX, double PanY, double Zoom) ZoomTo(
        Point screenAnchor,
        Point graphAnchor,
        double panX,
        double panY,
        double zoom)
    {
        var newZoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        return (
            screenAnchor.X - graphAnchor.X * newZoom,
            screenAnchor.Y - graphAnchor.Y * newZoom,
            newZoom);
    }

    /// <summary>
    /// Soft exponential zoom factor from wheel/trackpad deltas.
    /// Small continuous steps feel closer to macOS/iOS pinch-zoom than discrete jumps.
    /// </summary>
    public static double GetWheelZoomFactor(Vector wheelDelta)
    {
        var dy = Math.Clamp(wheelDelta.Y, -4.0, 4.0);
        // ~6% per full notch; fractional trackpad deltas stay gentle.
        return Math.Exp(dy * 0.058);
    }

    public static double StepToward(double current, double target, double deltaSeconds, double smoothing = ZoomSmoothing)
    {
        if (Math.Abs(target - current) < 0.00015)
            return target;

        var t = 1.0 - Math.Exp(-smoothing * Math.Max(deltaSeconds, 0));
        return current + (target - current) * t;
    }
}

using Avalonia;

namespace VisualStates.Controls;

/// <summary>
/// Coordinate transforms and zoom math for the graph canvas viewport.
/// Converts between screen and graph space and applies pan/zoom while keeping
/// anchor points fixed under the cursor.
/// </summary>
internal static class GraphViewport
{
    /// <summary>Minimum allowed zoom factor (25%).</summary>
    public const double MinZoom = 0.25;

    /// <summary>Maximum allowed zoom factor (300%).</summary>
    public const double MaxZoom = 3.0;

    /// <summary>Higher = snappier chase toward the target zoom (iOS-like ease-out).</summary>
    public const double ZoomSmoothing = 14.0;

    /// <summary>
    /// Converts a point from screen (control) coordinates to graph coordinates.
    /// </summary>
    /// <param name="screen">Point in screen space.</param>
    /// <param name="panX">Current horizontal pan offset in screen pixels.</param>
    /// <param name="panY">Current vertical pan offset in screen pixels.</param>
    /// <param name="zoom">Current zoom factor.</param>
    /// <returns>The corresponding point in graph space.</returns>
    public static Point ScreenToGraph(Point screen, double panX, double panY, double zoom) =>
        new((screen.X - panX) / zoom, (screen.Y - panY) / zoom);

    /// <summary>
    /// Converts a point from graph coordinates to screen (control) coordinates.
    /// </summary>
    /// <param name="graph">Point in graph space.</param>
    /// <param name="panX">Current horizontal pan offset in screen pixels.</param>
    /// <param name="panY">Current vertical pan offset in screen pixels.</param>
    /// <param name="zoom">Current zoom factor.</param>
    /// <returns>The corresponding point in screen space.</returns>
    public static Point GraphToScreen(Point graph, double panX, double panY, double zoom) =>
        new(graph.X * zoom + panX, graph.Y * zoom + panY);

    /// <summary>
    /// Applies a multiplicative zoom factor around a screen anchor, adjusting pan
    /// so the graph point under the anchor stays fixed.
    /// </summary>
    /// <param name="screenPoint">Screen-space anchor (typically the cursor position).</param>
    /// <param name="panX">Current horizontal pan offset.</param>
    /// <param name="panY">Current vertical pan offset.</param>
    /// <param name="zoom">Current zoom factor.</param>
    /// <param name="factor">Multiplicative zoom delta (e.g. from <see cref="GetWheelZoomFactor"/>).</param>
    /// <returns>Updated pan and zoom; unchanged when the new zoom would equal the current zoom.</returns>
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
    /// <param name="screenAnchor">Screen-space point that must remain stationary.</param>
    /// <param name="graphAnchor">Graph-space point that must remain under <paramref name="screenAnchor"/>.</param>
    /// <param name="panX">Current horizontal pan offset (unused for the calculation but kept for API symmetry).</param>
    /// <param name="panY">Current vertical pan offset (unused for the calculation but kept for API symmetry).</param>
    /// <param name="zoom">Desired zoom factor; clamped to <see cref="MinZoom"/> and <see cref="MaxZoom"/>.</param>
    /// <returns>Pan and zoom values that anchor <paramref name="graphAnchor"/> at <paramref name="screenAnchor"/>.</returns>
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
    /// <param name="wheelDelta">Raw wheel delta from a pointer event.</param>
    /// <returns>Multiplicative zoom factor to apply (1.0 = no change).</returns>
    public static double GetWheelZoomFactor(Vector wheelDelta)
    {
        var dy = Math.Clamp(wheelDelta.Y, -4.0, 4.0);
        // ~6% per full notch; fractional trackpad deltas stay gentle.
        return Math.Exp(dy * 0.058);
    }

    /// <summary>
    /// Exponential ease-out step toward a target value, suitable for smooth zoom animation.
    /// </summary>
    /// <param name="current">Current value.</param>
    /// <param name="target">Target value to approach.</param>
    /// <param name="deltaSeconds">Elapsed time since the last frame, in seconds.</param>
    /// <param name="smoothing">Response rate; defaults to <see cref="ZoomSmoothing"/>.</param>
    /// <returns>
    /// The interpolated value, or <paramref name="target"/> when already within
    /// <c>0.00015</c> of it.
    /// </returns>
    public static double StepToward(double current, double target, double deltaSeconds, double smoothing = ZoomSmoothing)
    {
        if (Math.Abs(target - current) < 0.00015)
            return target;

        var t = 1.0 - Math.Exp(-smoothing * Math.Max(deltaSeconds, 0));
        return current + (target - current) * t;
    }
}

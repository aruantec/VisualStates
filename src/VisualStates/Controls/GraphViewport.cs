using Avalonia;

namespace VisualStates.Controls;

internal static class GraphViewport
{
    public const double MinZoom = 0.25;
    public const double MaxZoom = 3.0;

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
        return (
            screenPoint.X - graph.X * newZoom,
            screenPoint.Y - graph.Y * newZoom,
            newZoom);
    }

    public static double GetWheelZoomFactor(Vector wheelDelta) =>
        Math.Pow(1.12, wheelDelta.Y);
}

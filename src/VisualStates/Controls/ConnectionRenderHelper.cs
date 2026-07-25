using Avalonia;
using Avalonia.Media;
using VisualStates.ViewModels;

namespace VisualStates.Controls;

internal readonly record struct GraphPin(
    StateBoxViewModel Box,
    StateStepViewModel? Step,
    bool IsOutput);

internal readonly record struct RoutePoint(double X, double Y, double Angle);

internal static class ConnectionRenderHelper
{
    public const double PinHitRadius = 10;
    public const double ConnectionHitRadiusScreen = 20;
    private const double StubLength = 28;
    private const double LaneSpacing = 22;
    private const double BelowClearance = 56;
    private const double BackwardThreshold = 12;
    private const int BezierSampleCount = 40;

    public static ConnectionViewModel? FindConnectionAtScreen(
        MainViewModel vm,
        Point screenPoint,
        double hitRadiusScreen = ConnectionHitRadiusScreen)
    {
        ConnectionViewModel? best = null;
        var bestDistance = double.MaxValue;

        for (var i = vm.Connections.Count - 1; i >= 0; i--)
        {
            var connection = vm.Connections[i];
            var (x1, y1) = connection.GetSourcePoint();
            var (x2, y2) = connection.GetTargetPoint();
            var distance = DistanceToConnectionScreen(
                x1, y1, x2, y2, i, vm.PanX, vm.PanY, vm.Zoom, screenPoint.X, screenPoint.Y);
            if (distance <= hitRadiusScreen && distance < bestDistance)
            {
                best = connection;
                bestDistance = distance;
            }
        }

        return best;
    }
    public static StreamGeometry CreateRoutedGeometry(
        double x1, double y1, double x2, double y2, int routeIndex = 0)
    {
        var points = GetPathPoints(x1, y1, x2, y2, routeIndex);
        return CreatePolylineGeometry(points);
    }

    public static RoutePoint GetRoutePoint(
        double x1, double y1, double x2, double y2, int routeIndex, double t = 0.55)
    {
        var points = GetPathPoints(x1, y1, x2, y2, routeIndex);
        return GetPointAlongPolyline(points, t);
    }

    public static double DistanceToConnectionScreen(
        double x1, double y1, double x2, double y2, int routeIndex,
        double panX, double panY, double zoom, double screenX, double screenY)
    {
        var points = GetPathPoints(x1, y1, x2, y2, routeIndex);
        if (points.Count < 2)
            return double.MaxValue;

        var min = double.MaxValue;
        for (var i = 0; i < points.Count - 1; i++)
        {
            var ax = points[i].X * zoom + panX;
            var ay = points[i].Y * zoom + panY;
            var bx = points[i + 1].X * zoom + panX;
            var by = points[i + 1].Y * zoom + panY;
            min = Math.Min(min, DistanceToSegment(screenX, screenY, new Point(ax, ay), new Point(bx, by)));
        }

        return min;
    }

    public static (double X, double Y) GetPinPosition(StateBoxViewModel box, StateStepViewModel? step, bool isOutput)
    {
        if (step is not null)
            return box.GetStepPinPosition(step, isOutput);

        return box.GetBoxPinPosition(isOutput);
    }

    public static bool IsNearPin(double x, double y, double pinX, double pinY, double radius = PinHitRadius)
    {
        var dx = x - pinX;
        var dy = y - pinY;
        return dx * dx + dy * dy <= radius * radius;
    }

    public static IReadOnlyList<Point> GetPathPoints(
        double x1, double y1, double x2, double y2, int routeIndex)
    {
        if (!IsBackwardRoute(x1, x2))
            return SampleForwardBezier(x1, y1, x2, y2, routeIndex);

        return BuildBelowRoutePoints(x1, y1, x2, y2, routeIndex);
    }

    private static bool IsBackwardRoute(double x1, double x2) => x2 < x1 - BackwardThreshold;

    private static List<Point> BuildBelowRoutePoints(
        double x1, double y1, double x2, double y2, int routeIndex)
    {
        var lane = routeIndex * LaneSpacing;
        var routeY = Math.Max(y1, y2) + BelowClearance + lane;
        var exitX = x1 + StubLength;
        var entryX = x2 - StubLength;

        return
        [
            new Point(x1, y1),
            new Point(exitX, y1),
            new Point(exitX, routeY),
            new Point(entryX, routeY),
            new Point(entryX, y2),
            new Point(x2, y2)
        ];
    }

    private static List<Point> SampleForwardBezier(
        double x1, double y1, double x2, double y2, int routeIndex)
    {
        var laneOffset = GetForwardLaneOffset(routeIndex);
        var horizontalOffset = Math.Max(40, Math.Abs(x2 - x1) * 0.5);
        var p0 = new Point(x1, y1);
        var p1 = new Point(x1 + horizontalOffset, y1 + laneOffset);
        var p2 = new Point(x2 - horizontalOffset, y2 + laneOffset);
        var p3 = new Point(x2, y2);

        var points = new List<Point> { p0 };
        for (var i = 1; i <= BezierSampleCount; i++)
        {
            var t = i / (double)BezierSampleCount;
            var u = 1 - t;
            points.Add(new Point(
                u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X,
                u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y));
        }

        return points;
    }

    private static StreamGeometry CreatePolylineGeometry(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        if (points.Count < 2)
            return geometry;

        using var ctx = geometry.Open();
        ctx.BeginFigure(points[0], false);
        for (var i = 1; i < points.Count; i++)
            ctx.LineTo(points[i]);

        return geometry;
    }

    private static RoutePoint GetPointAlongPolyline(IReadOnlyList<Point> points, double t)
    {
        if (points.Count < 2)
            return new RoutePoint(0, 0, 0);

        var lengths = new double[points.Count - 1];
        var total = 0.0;
        for (var i = 0; i < lengths.Length; i++)
        {
            var dx = points[i + 1].X - points[i].X;
            var dy = points[i + 1].Y - points[i].Y;
            lengths[i] = Math.Sqrt(dx * dx + dy * dy);
            total += lengths[i];
        }

        if (total <= 0)
            return new RoutePoint(points[^1].X, points[^1].Y, 0);

        var target = total * t;
        for (var i = 0; i < lengths.Length; i++)
        {
            if (target <= lengths[i] || i == lengths.Length - 1)
            {
                var localT = lengths[i] <= 0 ? 0 : target / lengths[i];
                var x = points[i].X + (points[i + 1].X - points[i].X) * localT;
                var y = points[i].Y + (points[i + 1].Y - points[i].Y) * localT;
                var angle = Math.Atan2(points[i + 1].Y - points[i].Y, points[i + 1].X - points[i].X);
                return new RoutePoint(x, y, angle);
            }

            target -= lengths[i];
        }

        return new RoutePoint(points[^1].X, points[^1].Y, 0);
    }

    private static double DistanceToSegment(double px, double py, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
        {
            var ex = px - a.X;
            var ey = py - a.Y;
            return Math.Sqrt(ex * ex + ey * ey);
        }

        var t = ((px - a.X) * dx + (py - a.Y) * dy) / (dx * dx + dy * dy);
        t = Math.Clamp(t, 0, 1);
        var projX = a.X + t * dx;
        var projY = a.Y + t * dy;
        var ox = px - projX;
        var oy = py - projY;
        return Math.Sqrt(ox * ox + oy * oy);
    }

    private static double GetForwardLaneOffset(int routeIndex)
    {
        if (routeIndex == 0)
            return 0;

        var lane = (routeIndex + 1) / 2;
        return (routeIndex % 2 == 0 ? lane : -lane) * 12;
    }
}

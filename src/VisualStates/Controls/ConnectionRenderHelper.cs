using Avalonia;
using Avalonia.Media;
using VisualStates.Core.Models;
using VisualStates.ViewModels;

namespace VisualStates.Controls;

/// <summary>
/// Identifies a connection endpoint on the graph canvas.
/// Pins are direction-agnostic: there is no output/input flag; <see cref="Side"/> describes
/// only the geometric edge of the host box, zone, or step where the pin sits.
/// </summary>
/// <param name="Box">The state box that owns this pin, when the endpoint is on a box body.</param>
/// <param name="Zone">The zone that owns this pin, when the endpoint is on a zone.</param>
/// <param name="Step">The step within <paramref name="Box"/> that owns this pin, when the endpoint is on a step pin.</param>
/// <param name="Side">The geometric edge (<see cref="PinSide.Left"/>, <see cref="PinSide.Right"/>, etc.) where the pin is anchored.</param>
internal readonly record struct GraphPin(
    StateBoxViewModel? Box,
    ZoneViewModel? Zone,
    StateStepViewModel? Step,
    PinSide Side)
{
    /// <summary>
    /// Gets a value indicating whether this pin belongs to a zone rather than a state box or step.
    /// </summary>
    public bool IsZone => Zone is not null;
}

/// <summary>
/// A position along a routed connection path together with the local tangent angle.
/// </summary>
/// <param name="X">Canvas X coordinate in diagram space.</param>
/// <param name="Y">Canvas Y coordinate in diagram space.</param>
/// <param name="Angle">Tangent direction at this point, in radians (same convention as <see cref="Math.Atan2(double, double)"/>).</param>
internal readonly record struct RoutePoint(double X, double Y, double Angle);

/// <summary>
/// An axis-aligned bounding rectangle used as a routing obstacle for state boxes.
/// </summary>
/// <param name="Left">Left edge X coordinate.</param>
/// <param name="Top">Top edge Y coordinate.</param>
/// <param name="Right">Right edge X coordinate.</param>
/// <param name="Bottom">Bottom edge Y coordinate.</param>
internal readonly record struct BoxRect(double Left, double Top, double Right, double Bottom);

/// <summary>
/// Computes orthogonal and Bezier connection paths, hit-testing geometry, and pin positions
/// for rendering state-machine wires on the canvas.
/// </summary>
internal static class ConnectionRenderHelper
{
    /// <summary>
    /// Radius in diagram space used for hit-testing whether the cursor is near a pin.
    /// </summary>
    public const double PinHitRadius = 10;

    /// <summary>
    /// Default radius in screen space used for hit-testing whether the cursor is near a connection wire.
    /// </summary>
    public const double ConnectionHitRadiusScreen = 20;

    /// <summary>Length of the straight stub segment extending outward from each pin before the route turns.</summary>
    private const double StubLength = 28;

    /// <summary>Minimum separation between parallel routing lanes when nudging to avoid overlap.</summary>
    private const double LaneSpacing = 22;

    /// <summary>Default clearance around box obstacles when testing whether a segment is blocked.</summary>
    private const double BoxClearance = 24;

    /// <summary>Tighter clearance for the bridge channel between stacked boxes.</summary>
    private const double BridgeClearance = 4;

    /// <summary>Minimum span required before <see cref="TryFindChannel"/> searches for an in-band bridge channel.</summary>
    private const double MinBridgeGap = 10;

    /// <summary>Distance threshold below which opposite-facing horizontal or vertical pins are treated as backward/overlapping.</summary>
    private const double BackwardThreshold = 12;

    /// <summary>Maximum pin offset (in the cross axis) for treating two pins as aligned enough for a Bezier shortcut.</summary>
    private const double AlignThreshold = 10;

    /// <summary>Number of line segments used when approximating a cubic Bezier as a polyline.</summary>
    private const int BezierSampleCount = 40;

    /// <summary>
    /// Finds the topmost connection whose routed path lies within <paramref name="hitRadiusScreen"/>
    /// of <paramref name="screenPoint"/> in screen coordinates.
    /// </summary>
    /// <param name="vm">The main view model containing connections, pan, and zoom state.</param>
    /// <param name="screenPoint">Cursor position in screen (control) coordinates.</param>
    /// <param name="hitRadiusScreen">Maximum distance in screen pixels from the wire polyline.</param>
    /// <returns>The closest matching <see cref="ConnectionViewModel"/>, or <see langword="null"/> if none are within range.</returns>
    public static ConnectionViewModel? FindConnectionAtScreen(
        MainViewModel vm,
        Point screenPoint,
        double hitRadiusScreen = ConnectionHitRadiusScreen)
    {
        ConnectionViewModel? best = null;
        var bestDistance = double.MaxValue;
        var paths = BuildAllConnectionPaths(vm);

        for (var i = vm.Connections.Count - 1; i >= 0; i--)
        {
            var distance = DistanceToPolylineScreen(
                paths[i], vm.PanX, vm.PanY, vm.Zoom, screenPoint.X, screenPoint.Y);
            if (distance <= hitRadiusScreen && distance < bestDistance)
            {
                best = vm.Connections[i];
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// Routes every connection in order, reserving vertical and horizontal lanes so
    /// later wires step aside instead of stacking on the same stub column.
    /// </summary>
    /// <param name="vm">The main view model whose connections and boxes define the diagram.</param>
    /// <returns>One polyline point list per connection, in the same order as <see cref="MainViewModel.Connections"/>.</returns>
    public static IReadOnlyList<IReadOnlyList<Point>> BuildAllConnectionPaths(MainViewModel vm)
    {
        var obstacles = BuildObstacles(vm);
        var reservedV = new List<double>();
        var reservedH = new List<double>();
        var paths = new List<IReadOnlyList<Point>>(vm.Connections.Count);

        for (var i = 0; i < vm.Connections.Count; i++)
        {
            var connection = vm.Connections[i];
            var sp = connection.GetSourcePoint();
            var tp = connection.GetTargetPoint();
            // Keep endpoint boxes in the obstacle list so opposite-side bridges
            // thread the gap between nodes instead of cutting through them.
            var points = GetPathPoints(
                sp.X, sp.Y, connection.SourceSide,
                tp.X, tp.Y, connection.TargetSide,
                i, obstacles, reservedV, reservedH);
            paths.Add(points);
            AddLanesFromPath(points, reservedV, reservedH);
        }

        return paths;
    }

    /// <summary>
    /// Builds a <see cref="StreamGeometry"/> polyline from an already-computed route.
    /// </summary>
    /// <param name="points">Polyline vertices in diagram space.</param>
    /// <returns>A geometry suitable for drawing the connection stroke.</returns>
    public static StreamGeometry CreateRoutedGeometry(IReadOnlyList<Point> points)
        => CreatePolylineGeometry(points);

    /// <summary>
    /// Computes a routed path between two pins and returns it as a <see cref="StreamGeometry"/> polyline.
    /// </summary>
    /// <param name="x1">Source pin X in diagram space.</param>
    /// <param name="y1">Source pin Y in diagram space.</param>
    /// <param name="sourceSide">Geometric side of the source pin.</param>
    /// <param name="x2">Target pin X in diagram space.</param>
    /// <param name="y2">Target pin Y in diagram space.</param>
    /// <param name="targetSide">Geometric side of the target pin.</param>
    /// <param name="routeIndex">Index used to stagger overlapping Bezier lanes.</param>
    /// <param name="obstacles">Optional box rectangles to avoid; defaults to none.</param>
    /// <param name="reservedVerticalLanes">Optional X coordinates already claimed by prior routes.</param>
    /// <param name="reservedHorizontalLanes">Optional Y coordinates already claimed by prior routes.</param>
    /// <returns>A geometry representing the routed connection.</returns>
    public static StreamGeometry CreateRoutedGeometry(
        double x1, double y1, PinSide sourceSide,
        double x2, double y2, PinSide targetSide,
        int routeIndex = 0,
        IReadOnlyList<BoxRect>? obstacles = null,
        IReadOnlyList<double>? reservedVerticalLanes = null,
        IReadOnlyList<double>? reservedHorizontalLanes = null)
    {
        var points = GetPathPoints(
            x1, y1, sourceSide, x2, y2, targetSide, routeIndex, obstacles,
            reservedVerticalLanes, reservedHorizontalLanes);
        return CreatePolylineGeometry(points);
    }

    /// <summary>
    /// Rubber-band preview while dragging a new connection. Uses a distance-scaled
    /// Bezier (not orthogonal stubs) so a near-pin cursor never draws a square or lasso shape.
    /// </summary>
    /// <param name="x1">Source pin X in diagram space.</param>
    /// <param name="y1">Source pin Y in diagram space.</param>
    /// <param name="sourceSide">Geometric side of the source pin.</param>
    /// <param name="x2">Drag cursor or target pin X in diagram space.</param>
    /// <param name="y2">Drag cursor or target pin Y in diagram space.</param>
    /// <param name="targetSide">Geometric side of the tentative target pin.</param>
    /// <returns>A smooth preview geometry following a cubic Bezier approximation.</returns>
    public static StreamGeometry CreateDragPreviewGeometry(
        double x1, double y1, PinSide sourceSide,
        double x2, double y2, PinSide targetSide)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var distSq = dx * dx + dy * dy;
        if (distSq < 4)
            return CreatePolylineGeometry([new Point(x1, y1), new Point(x2, y2)]);

        var dist = Math.Sqrt(distSq);
        // Cap stub influence by how far the cursor has moved.
        var offset = Math.Min(StubLength, dist * 0.35);
        var srcDir = SignOf(sourceSide);
        var tgtDir = SignOf(targetSide);
        var sourceHorizontal = sourceSide is PinSide.Left or PinSide.Right or PinSide.Error;

        var p0 = new Point(x1, y1);
        var p3 = new Point(x2, y2);
        Point p1, p2;
        if (sourceHorizontal)
        {
            p1 = new Point(x1 + srcDir * offset, y1);
            p2 = new Point(x2 + tgtDir * offset, y2);
        }
        else
        {
            p1 = new Point(x1, y1 + srcDir * offset);
            p2 = new Point(x2, y2 + tgtDir * offset);
        }

        return CreatePolylineGeometry(SampleCubicBezier(p0, p1, p2, p3));
    }

    /// <summary>
    /// Returns a point and tangent angle at normalized distance <paramref name="t"/> along a polyline route.
    /// </summary>
    /// <param name="points">Polyline vertices in diagram space.</param>
    /// <param name="t">Normalized distance along the path, in the range 0–1.</param>
    /// <returns>A <see cref="RoutePoint"/> at the requested position.</returns>
    public static RoutePoint GetRoutePoint(IReadOnlyList<Point> points, double t = 0.55)
        => GetPointAlongPolyline(points, t);

    /// <summary>
    /// Routes between two pins and returns a point and tangent at normalized distance <paramref name="t"/> along the path.
    /// </summary>
    /// <param name="x1">Source pin X in diagram space.</param>
    /// <param name="y1">Source pin Y in diagram space.</param>
    /// <param name="sourceSide">Geometric side of the source pin.</param>
    /// <param name="x2">Target pin X in diagram space.</param>
    /// <param name="y2">Target pin Y in diagram space.</param>
    /// <param name="targetSide">Geometric side of the target pin.</param>
    /// <param name="routeIndex">Index used to stagger overlapping Bezier lanes.</param>
    /// <param name="obstacles">Optional box rectangles to avoid.</param>
    /// <param name="t">Normalized distance along the path, in the range 0–1.</param>
    /// <returns>A <see cref="RoutePoint"/> suitable for placing labels or arrowheads.</returns>
    public static RoutePoint GetRoutePoint(
        double x1, double y1, PinSide sourceSide,
        double x2, double y2, PinSide targetSide,
        int routeIndex,
        IReadOnlyList<BoxRect>? obstacles = null, double t = 0.55)
    {
        var points = GetPathPoints(x1, y1, sourceSide, x2, y2, targetSide, routeIndex, obstacles);
        return GetPointAlongPolyline(points, t);
    }

    /// <summary>
    /// Computes the shortest distance from a screen-space point to a polyline after applying pan and zoom.
    /// </summary>
    /// <param name="points">Polyline vertices in diagram space.</param>
    /// <param name="panX">Horizontal pan offset in screen pixels.</param>
    /// <param name="panY">Vertical pan offset in screen pixels.</param>
    /// <param name="zoom">Diagram-to-screen scale factor.</param>
    /// <param name="screenX">Test point X in screen coordinates.</param>
    /// <param name="screenY">Test point Y in screen coordinates.</param>
    /// <returns>Distance in screen pixels, or <see cref="double.MaxValue"/> when <paramref name="points"/> has fewer than two vertices.</returns>
    public static double DistanceToPolylineScreen(
        IReadOnlyList<Point> points,
        double panX, double panY, double zoom, double screenX, double screenY)
    {
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

    /// <summary>
    /// Routes between two pins and returns the screen-space distance from a point to that path.
    /// </summary>
    /// <param name="x1">Source pin X in diagram space.</param>
    /// <param name="y1">Source pin Y in diagram space.</param>
    /// <param name="sourceSide">Geometric side of the source pin.</param>
    /// <param name="x2">Target pin X in diagram space.</param>
    /// <param name="y2">Target pin Y in diagram space.</param>
    /// <param name="targetSide">Geometric side of the target pin.</param>
    /// <param name="routeIndex">Index used to stagger overlapping Bezier lanes.</param>
    /// <param name="panX">Horizontal pan offset in screen pixels.</param>
    /// <param name="panY">Vertical pan offset in screen pixels.</param>
    /// <param name="zoom">Diagram-to-screen scale factor.</param>
    /// <param name="screenX">Test point X in screen coordinates.</param>
    /// <param name="screenY">Test point Y in screen coordinates.</param>
    /// <param name="obstacles">Optional box rectangles to avoid.</param>
    /// <returns>Distance in screen pixels from <paramref name="screenX"/>/<paramref name="screenY"/> to the routed wire.</returns>
    public static double DistanceToConnectionScreen(
        double x1, double y1, PinSide sourceSide,
        double x2, double y2, PinSide targetSide,
        int routeIndex,
        double panX, double panY, double zoom, double screenX, double screenY,
        IReadOnlyList<BoxRect>? obstacles = null)
    {
        var points = GetPathPoints(x1, y1, sourceSide, x2, y2, targetSide, routeIndex, obstacles);
        return DistanceToPolylineScreen(points, panX, panY, zoom, screenX, screenY);
    }

    /// <summary>
    /// Resolves the diagram-space coordinates of a pin on a box body or on a specific step.
    /// </summary>
    /// <param name="box">The state box that owns the pin.</param>
    /// <param name="step">The step pin to use, or <see langword="null"/> for a box-level pin.</param>
    /// <param name="side">Geometric side of the pin.</param>
    /// <returns>The pin position as (<c>X</c>, <c>Y</c>) in diagram space.</returns>
    public static (double X, double Y) GetPinPosition(StateBoxViewModel box, StateStepViewModel? step, PinSide side)
    {
        if (step is not null)
            return box.GetStepPinPosition(step, side);

        return box.GetBoxPinPosition(side);
    }

    /// <summary>
    /// Determines whether a diagram-space point lies within a circular hit region around a pin.
    /// </summary>
    /// <param name="x">Test point X in diagram space.</param>
    /// <param name="y">Test point Y in diagram space.</param>
    /// <param name="pinX">Pin center X in diagram space.</param>
    /// <param name="pinY">Pin center Y in diagram space.</param>
    /// <param name="radius">Hit radius in diagram space; defaults to <see cref="PinHitRadius"/>.</param>
    /// <returns><see langword="true"/> when the test point is inside the hit circle.</returns>
    public static bool IsNearPin(double x, double y, double pinX, double pinY, double radius = PinHitRadius)
    {
        var dx = x - pinX;
        var dy = y - pinY;
        return dx * dx + dy * dy <= radius * radius;
    }

    /// <summary>
    /// Computes the polyline vertices for a connection between two pins, choosing orthogonal
    /// or Bezier routing based on pin sides, obstacles, and reserved lanes.
    /// </summary>
    /// <param name="x1">Source pin X in diagram space.</param>
    /// <param name="y1">Source pin Y in diagram space.</param>
    /// <param name="sourceSide">Geometric side of the source pin.</param>
    /// <param name="x2">Target pin X in diagram space.</param>
    /// <param name="y2">Target pin Y in diagram space.</param>
    /// <param name="targetSide">Geometric side of the target pin.</param>
    /// <param name="routeIndex">Index used to stagger overlapping Bezier lanes.</param>
    /// <param name="obstacles">Box rectangles to avoid; treated as empty when <see langword="null"/>.</param>
    /// <param name="reservedVerticalLanes">X coordinates already claimed by prior routes.</param>
    /// <param name="reservedHorizontalLanes">Y coordinates already claimed by prior routes.</param>
    /// <returns>An ordered list of polyline points from source to target.</returns>
    public static IReadOnlyList<Point> GetPathPoints(
        double x1, double y1, PinSide sourceSide,
        double x2, double y2, PinSide targetSide,
        int routeIndex,
        IReadOnlyList<BoxRect>? obstacles = null,
        IReadOnlyList<double>? reservedVerticalLanes = null,
        IReadOnlyList<double>? reservedHorizontalLanes = null)
    {
        obstacles ??= [];
        reservedVerticalLanes ??= [];
        reservedHorizontalLanes ??= [];
        var sourceHorizontal = sourceSide is PinSide.Left or PinSide.Right or PinSide.Error;
        var targetHorizontal = targetSide is PinSide.Left or PinSide.Right or PinSide.Error;

        if (sourceHorizontal && targetHorizontal)
        {
            return RouteHorizontal(
                x1, y1, sourceSide, x2, y2, targetSide, routeIndex, obstacles,
                reservedVerticalLanes, reservedHorizontalLanes);
        }

        if (!sourceHorizontal && !targetHorizontal)
        {
            return RouteVertical(
                x1, y1, sourceSide, x2, y2, targetSide, routeIndex, obstacles,
                reservedVerticalLanes, reservedHorizontalLanes);
        }

        return RouteElbow(
            x1, y1, sourceSide, x2, y2, targetSide, routeIndex, obstacles,
            reservedVerticalLanes, reservedHorizontalLanes);
    }

    /// <summary>
    /// Builds axis-aligned obstacle rectangles from all state boxes in the view model.
    /// </summary>
    /// <param name="vm">The main view model containing box layout information.</param>
    /// <returns>One <see cref="BoxRect"/> per box, including expanded height for steps.</returns>
    public static IReadOnlyList<BoxRect> BuildObstacles(MainViewModel vm)
    {
        var obstacles = new List<BoxRect>(vm.Boxes.Count);
        foreach (var box in vm.Boxes)
        {
            var top = box.Y;
            var bottom = box.Y + box.GetTotalHeight();
            obstacles.Add(new BoxRect(box.X, top, box.X + box.Width, bottom));
        }

        return obstacles;
    }

    /// <summary>
    /// Returns +1 or -1 for the outward stub direction from a pin based on its geometric side.
    /// This describes exit direction from the box edge, not source-versus-target connection flow.
    /// </summary>
    /// <param name="side">Geometric pin side.</param>
    /// <returns>+1 for <see cref="PinSide.Right"/>, <see cref="PinSide.Bottom"/>, and <see cref="PinSide.Error"/>; -1 for <see cref="PinSide.Left"/> and <see cref="PinSide.Top"/>; 0 otherwise.</returns>
    private static int SignOf(PinSide side) => side switch
    {
        PinSide.Right or PinSide.Bottom or PinSide.Error => +1,
        PinSide.Left or PinSide.Top => -1,
        _ => 0
    };

    /// <summary>
    /// Routes a connection between two horizontally oriented pins, preferring a Bezier when aligned
    /// and falling back to <see cref="OrthogonalHorizontal"/> otherwise.
    /// </summary>
    private static List<Point> RouteHorizontal(
        double x1, double y1, PinSide sourceSide,
        double x2, double y2, PinSide targetSide,
        int routeIndex, IReadOnlyList<BoxRect> obstacles,
        IReadOnlyList<double> reservedVerticalLanes,
        IReadOnlyList<double> reservedHorizontalLanes)
    {
        // Bezier only when pins are roughly level — otherwise use orthogonal
        // corners (same style as stacked Right→Left wires like State 2 → State 3).
        var aligned = Math.Abs(y2 - y1) < AlignThreshold;
        if (aligned
            && !NeedsOrthogonalHorizontal(x1, sourceSide, x2, targetSide)
            && HasBezierRoomHorizontal(x1, sourceSide, x2, targetSide))
        {
            return HorizontalBezier(x1, y1, sourceSide, x2, y2, targetSide, routeIndex);
        }

        return OrthogonalHorizontal(
            x1, y1, sourceSide, x2, y2, targetSide, routeIndex, obstacles,
            reservedVerticalLanes, reservedHorizontalLanes);
    }

    /// <summary>
    /// Returns whether horizontally oriented pins face backward relative to each other beyond <see cref="BackwardThreshold"/>.
    /// </summary>
    private static bool NeedsOrthogonalHorizontal(double x1, PinSide sourceSide, double x2, PinSide targetSide)
    {
        var sourceForward = sourceSide is PinSide.Right or PinSide.Error;
        var targetForward = targetSide is PinSide.Right or PinSide.Error;
        if (sourceForward && targetForward)
            return x2 < x1 - BackwardThreshold;
        if (!sourceForward && !targetForward)
            return x1 < x2 - BackwardThreshold;
        return false;
    }

    /// <summary>
    /// Returns whether stub exit points along X leave enough gap for a horizontal Bezier between the pins.
    /// </summary>
    private static bool HasBezierRoomHorizontal(double x1, PinSide sourceSide, double x2, PinSide targetSide)
    {
        const double BezierMinGap = 16;
        var sourceExit = x1 + SignOf(sourceSide) * BezierMinGap;
        var targetEntry = x2 + SignOf(targetSide) * BezierMinGap;
        return sourceExit <= targetEntry;
    }

    /// <summary>
    /// Routes a connection between two vertically oriented pins, preferring a Bezier when aligned
    /// and falling back to <see cref="OrthogonalVertical"/> otherwise.
    /// </summary>
    private static List<Point> RouteVertical(
        double x1, double y1, PinSide sourceSide,
        double x2, double y2, PinSide targetSide,
        int routeIndex, IReadOnlyList<BoxRect> obstacles,
        IReadOnlyList<double> reservedVerticalLanes,
        IReadOnlyList<double> reservedHorizontalLanes)
    {
        var aligned = Math.Abs(x2 - x1) < AlignThreshold;
        if (aligned
            && !NeedsOrthogonalVertical(y1, sourceSide, y2, targetSide)
            && HasBezierRoomVertical(y1, sourceSide, y2, targetSide))
        {
            return VerticalBezier(x1, y1, sourceSide, x2, y2, targetSide, routeIndex);
        }

        return OrthogonalVertical(
            x1, y1, sourceSide, x2, y2, targetSide, routeIndex, obstacles,
            reservedVerticalLanes, reservedHorizontalLanes);
    }

    /// <summary>
    /// Returns whether vertically oriented pins face backward relative to each other beyond <see cref="BackwardThreshold"/>.
    /// </summary>
    private static bool NeedsOrthogonalVertical(double y1, PinSide sourceSide, double y2, PinSide targetSide)
    {
        var sourceForward = sourceSide == PinSide.Bottom;
        var targetForward = targetSide == PinSide.Bottom;
        if (sourceForward && targetForward)
            return y2 < y1 - BackwardThreshold;
        if (!sourceForward && !targetForward)
            return y1 < y2 - BackwardThreshold;
        return false;
    }

    /// <summary>
    /// Returns whether stub exit points along Y leave enough gap for a vertical Bezier between the pins.
    /// </summary>
    private static bool HasBezierRoomVertical(double y1, PinSide sourceSide, double y2, PinSide targetSide)
    {
        const double BezierMinGap = 16;
        var sourceExit = y1 + SignOf(sourceSide) * BezierMinGap;
        var targetEntry = y2 + SignOf(targetSide) * BezierMinGap;
        return sourceExit <= targetEntry;
    }

    /// <summary>
    /// Builds an orthogonal polyline for two horizontally oriented pins, handling same-facing U-routes,
    /// opposite-facing two-corner paths, and bridge segments when stubs cross.
    /// </summary>
    private static List<Point> OrthogonalHorizontal(
        double x1, double y1, PinSide sourceSide,
        double x2, double y2, PinSide targetSide,
        int routeIndex, IReadOnlyList<BoxRect> obstacles,
        IReadOnlyList<double> reservedVerticalLanes,
        IReadOnlyList<double> reservedHorizontalLanes)
    {
        var srcDir = SignOf(sourceSide);
        var tgtDir = SignOf(targetSide);
        var exitX = x1 + srcDir * StubLength;
        var entryX = x2 + tgtDir * StubLength;

        // Same-facing (Right→Right / Left→Left): single-column U when pins are
        // roughly stacked/aligned. Never invent a mid-path Z-jog.
        if (srcDir == tgtDir)
        {
            var col = srcDir > 0 ? Math.Max(exitX, entryX) : Math.Min(exitX, entryX);
            col = NudgeLane(col, srcDir, reservedVerticalLanes);

            // Stacked / nearby nodes → always a clean U on one outer column.
            // Far targets behind the facing direction (Left→Left into a zone on the
            // right) need a wrap; |x1-x2| catches that without false-wrapping when
            // lane nudging pushes the column outward.
            if (Math.Abs(x1 - x2) <= StubLength * 4)
            {
                return
                [
                    new Point(x1, y1),
                    new Point(col, y1),
                    new Point(col, y2),
                    new Point(x2, y2)
                ];
            }

            var outX = NudgeLane(exitX, srcDir, reservedVerticalLanes);
            var inX = NudgeLane(entryX, srcDir, reservedVerticalLanes);
            var routeY = ClearBridgeY(y1, y2, outX, inX, obstacles, reservedHorizontalLanes);
            return
            [
                new Point(x1, y1),
                new Point(outX, y1),
                new Point(outX, routeY),
                new Point(inX, routeY),
                new Point(inX, y2),
                new Point(x2, y2)
            ];
        }

        // Opposite-facing with room between stubs: 2-corner on one column.
        var forward = srcDir > 0 ? exitX <= entryX + 0.5 : exitX >= entryX - 0.5;
        if (forward)
        {
            var col = NudgeLane(exitX, srcDir, reservedVerticalLanes);
            if (!VerticalSegmentBlocked(col, y1, y2, obstacles))
            {
                return
                [
                    new Point(x1, y1),
                    new Point(col, y1),
                    new Point(col, y2),
                    new Point(x2, y2)
                ];
            }

            col = NudgeLane(entryX, -tgtDir, reservedVerticalLanes);
            return
            [
                new Point(x1, y1),
                new Point(col, y1),
                new Point(col, y2),
                new Point(x2, y2)
            ];
        }

        // Opposite-facing, stubs cross (Right→Left on a vertical stack).
        var outCol = NudgeLane(exitX, srcDir, reservedVerticalLanes);
        var inCol = NudgeLane(entryX, -tgtDir, reservedVerticalLanes);
        var bridgeY = ClearBridgeY(y1, y2, outCol, inCol, obstacles, reservedHorizontalLanes);
        return
        [
            new Point(x1, y1),
            new Point(outCol, y1),
            new Point(outCol, bridgeY),
            new Point(inCol, bridgeY),
            new Point(inCol, y2),
            new Point(x2, y2)
        ];
    }

    /// <summary>
    /// Builds an orthogonal polyline for two vertically oriented pins, mirroring
    /// <see cref="OrthogonalHorizontal"/> in the vertical axis.
    /// </summary>
    private static List<Point> OrthogonalVertical(
        double x1, double y1, PinSide sourceSide,
        double x2, double y2, PinSide targetSide,
        int routeIndex, IReadOnlyList<BoxRect> obstacles,
        IReadOnlyList<double> reservedVerticalLanes,
        IReadOnlyList<double> reservedHorizontalLanes)
    {
        var srcDir = SignOf(sourceSide);
        var tgtDir = SignOf(targetSide);
        var exitY = y1 + srcDir * StubLength;
        var entryY = y2 + tgtDir * StubLength;

        if (srcDir == tgtDir)
        {
            var row = srcDir > 0 ? Math.Max(exitY, entryY) : Math.Min(exitY, entryY);
            row = NudgeLane(row, srcDir, reservedHorizontalLanes);

            if (Math.Abs(y1 - y2) <= StubLength * 4)
            {
                return
                [
                    new Point(x1, y1),
                    new Point(x1, row),
                    new Point(x2, row),
                    new Point(x2, y2)
                ];
            }

            var outY = NudgeLane(exitY, srcDir, reservedHorizontalLanes);
            var inY = NudgeLane(entryY, srcDir, reservedHorizontalLanes);
            var routeX = ClearBridgeX(x1, x2, outY, inY, obstacles, reservedVerticalLanes);
            return
            [
                new Point(x1, y1),
                new Point(x1, outY),
                new Point(routeX, outY),
                new Point(routeX, inY),
                new Point(x2, inY),
                new Point(x2, y2)
            ];
        }

        var forward = srcDir > 0 ? exitY <= entryY + 0.5 : exitY >= entryY - 0.5;
        if (forward)
        {
            var row = NudgeLane(exitY, srcDir, reservedHorizontalLanes);
            if (!HorizontalSegmentBlocked(row, x1, x2, obstacles))
            {
                return
                [
                    new Point(x1, y1),
                    new Point(x1, row),
                    new Point(x2, row),
                    new Point(x2, y2)
                ];
            }

            row = NudgeLane(entryY, -tgtDir, reservedHorizontalLanes);
            return
            [
                new Point(x1, y1),
                new Point(x1, row),
                new Point(x2, row),
                new Point(x2, y2)
            ];
        }

        var outRow = NudgeLane(exitY, srcDir, reservedHorizontalLanes);
        var inRow = NudgeLane(entryY, -tgtDir, reservedHorizontalLanes);
        var bridgeX = ClearBridgeX(x1, x2, outRow, inRow, obstacles, reservedVerticalLanes);
        return
        [
            new Point(x1, y1),
            new Point(x1, outRow),
            new Point(bridgeX, outRow),
            new Point(bridgeX, inRow),
            new Point(x2, inRow),
            new Point(x2, y2)
        ];
    }

    /// <summary>
    /// Routes a mixed-orientation connection (one horizontal pin, one vertical pin) with a single elbow turn.
    /// </summary>
    private static List<Point> RouteElbow(
        double x1, double y1, PinSide sourceSide,
        double x2, double y2, PinSide targetSide,
        int routeIndex, IReadOnlyList<BoxRect> obstacles,
        IReadOnlyList<double> reservedVerticalLanes,
        IReadOnlyList<double> reservedHorizontalLanes)
    {
        var srcDir = SignOf(sourceSide);
        var tgtDir = SignOf(targetSide);
        var sourceHorizontal = sourceSide is PinSide.Left or PinSide.Right or PinSide.Error;

        if (sourceHorizontal)
        {
            var turnX = NudgeLane(x1 + srcDir * StubLength, srcDir, reservedVerticalLanes);
            var entryY = y2 + tgtDir * StubLength;
            return
            [
                new Point(x1, y1),
                new Point(turnX, y1),
                new Point(turnX, entryY),
                new Point(x2, entryY),
                new Point(x2, y2)
            ];
        }

        var turnY = NudgeLane(y1 + srcDir * StubLength, srcDir, reservedHorizontalLanes);
        var entryX = x2 + tgtDir * StubLength;
        return
        [
            new Point(x1, y1),
            new Point(x1, turnY),
            new Point(entryX, turnY),
            new Point(entryX, y2),
            new Point(x2, y2)
        ];
    }

    /// <summary>Push a lane outward until it is free of previously reserved wires.</summary>
    private static double NudgeLane(double preferred, int dir, IReadOnlyList<double> reserved)
    {
        if (dir == 0)
            dir = 1;

        var value = preferred;
        for (var i = 0; i < 20; i++)
        {
            if (!IsLaneReserved(value, reserved))
                return value;
            value += dir * LaneSpacing;
        }

        return preferred;
    }

    /// <summary>
    /// Finds a clear horizontal bridge Y between two vertical columns, preferring an in-band channel
    /// and otherwise wrapping above or below the pin span.
    /// </summary>
    private static double ClearBridgeY(
        double y1, double y2, double leftX, double rightX,
        IReadOnlyList<BoxRect> obstacles,
        IReadOnlyList<double> reserved)
    {
        var lo = Math.Min(y1, y2);
        var hi = Math.Max(y1, y2);
        var mid = (y1 + y2) * 0.5;

        if (TryFindChannel(
                lo, hi, mid, reserved,
                y => !HorizontalSegmentBlocked(y, leftX, rightX, obstacles, BridgeClearance),
                out var gapY))
        {
            return gapY;
        }

        var above = mid;
        var below = mid;
        for (var i = 0; i < 40; i++)
        {
            var y = lo - BoxClearance - i * 4;
            if (!HorizontalSegmentBlocked(y, leftX, rightX, obstacles) && !IsLaneReserved(y, reserved))
            {
                above = y;
                break;
            }
        }

        for (var i = 0; i < 40; i++)
        {
            var y = hi + BoxClearance + i * 4;
            if (!HorizontalSegmentBlocked(y, leftX, rightX, obstacles) && !IsLaneReserved(y, reserved))
            {
                below = y;
                break;
            }
        }

        return Math.Abs(above - mid) <= Math.Abs(below - mid) ? above : below;
    }

    /// <summary>
    /// Finds a clear vertical bridge X between two horizontal rows, preferring an in-band channel
    /// and otherwise wrapping left or right of the pin span.
    /// </summary>
    private static double ClearBridgeX(
        double x1, double x2, double topY, double bottomY,
        IReadOnlyList<BoxRect> obstacles,
        IReadOnlyList<double> reserved)
    {
        var lo = Math.Min(x1, x2);
        var hi = Math.Max(x1, x2);
        var mid = (x1 + x2) * 0.5;

        if (TryFindChannel(
                lo, hi, mid, reserved,
                x => !VerticalSegmentBlocked(x, topY, bottomY, obstacles, BridgeClearance),
                out var gapX))
        {
            return gapX;
        }

        var left = mid;
        var right = mid;
        for (var i = 0; i < 40; i++)
        {
            var x = lo - BoxClearance - i * 4;
            if (!VerticalSegmentBlocked(x, topY, bottomY, obstacles) && !IsLaneReserved(x, reserved))
            {
                left = x;
                break;
            }
        }

        for (var i = 0; i < 40; i++)
        {
            var x = hi + BoxClearance + i * 4;
            if (!VerticalSegmentBlocked(x, topY, bottomY, obstacles) && !IsLaneReserved(x, reserved))
            {
                right = x;
                break;
            }
        }

        return Math.Abs(left - mid) <= Math.Abs(right - mid) ? left : right;
    }

    /// <summary>
    /// Records vertical and horizontal lane coordinates from long axis-aligned segments of a routed path
    /// so subsequent connections can nudge away from them.
    /// </summary>
    private static void AddLanesFromPath(
        IReadOnlyList<Point> points, List<double> reservedVertical, List<double> reservedHorizontal)
    {
        for (var i = 0; i < points.Count - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            var dx = Math.Abs(a.X - b.X);
            var dy = Math.Abs(a.Y - b.Y);
            if (dx < 0.5 && dy >= StubLength * 0.5)
                reservedVertical.Add(a.X);
            else if (dy < 0.5 && dx >= StubLength * 0.5)
                reservedHorizontal.Add(a.Y);
        }
    }

    /// <summary>
    /// Returns whether a coordinate is within <see cref="LaneSpacing"/> of any reserved lane value.
    /// </summary>
    private static bool IsLaneReserved(double value, IReadOnlyList<double> reserved)
    {
        foreach (var lane in reserved)
        {
            if (Math.Abs(lane - value) < LaneSpacing - 1)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Finds a clear value in [lo, hi] closest to <paramref name="target"/>, preferring
    /// geometric gaps between obstacles over wrapping outside the span.
    /// </summary>
    /// <param name="lo">Lower bound of the search interval.</param>
    /// <param name="hi">Upper bound of the search interval.</param>
    /// <param name="target">Preferred coordinate within the interval.</param>
    /// <param name="reserved">Lane coordinates that must not be reused.</param>
    /// <param name="isClear">Predicate that returns <see langword="true"/> when a candidate coordinate is unobstructed.</param>
    /// <param name="channel">When this method returns <see langword="true"/>, receives the chosen coordinate.</param>
    /// <returns><see langword="true"/> when a suitable channel was found inside the interval.</returns>
    private static bool TryFindChannel(
        double lo, double hi, double target,
        IReadOnlyList<double> reserved,
        Func<double, bool> isClear,
        out double channel)
    {
        channel = target;
        if (hi - lo < MinBridgeGap)
            return false;

        double? best = null;
        var bestDist = double.MaxValue;

        // Sample the band; also probe just outside each reserved lane.
        var samples = Math.Max(8, (int)((hi - lo) / 4));
        for (var i = 0; i <= samples; i++)
        {
            var y = lo + (hi - lo) * i / samples;
            Consider(y);
        }

        Consider(target);
        foreach (var lane in reserved)
        {
            Consider(lane + LaneSpacing);
            Consider(lane - LaneSpacing);
        }

        if (best is null)
            return false;

        channel = best.Value;
        return true;

        void Consider(double value)
        {
            if (value < lo || value > hi)
                return;
            if (!isClear(value))
                return;
            if (IsLaneReserved(value, reserved))
                return;
            var dist = Math.Abs(value - target);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = value;
            }
        }
    }

    /// <summary>
    /// Searches along X for a vertical lane that is clear of obstacles and reserved wires.
    /// </summary>
    private static double PickVerticalLane(
        double preferredX, double y1, double y2,
        IReadOnlyList<BoxRect> obstacles,
        IReadOnlyList<double> reserved,
        int searchDir)
    {
        if (searchDir == 0)
            searchDir = 1;

        for (var i = 0; i < 24; i++)
        {
            var x = preferredX + searchDir * i * LaneSpacing;
            if (VerticalSegmentBlocked(x, y1, y2, obstacles))
                continue;
            if (IsLaneReserved(x, reserved))
                continue;
            return x;
        }

        for (var i = 1; i < 24; i++)
        {
            var x = preferredX - searchDir * i * LaneSpacing;
            if (VerticalSegmentBlocked(x, y1, y2, obstacles))
                continue;
            if (IsLaneReserved(x, reserved))
                continue;
            return x;
        }

        return preferredX;
    }

    /// <summary>
    /// Searches along Y for a horizontal lane that is clear of obstacles and reserved wires.
    /// </summary>
    private static double PickHorizontalLane(
        double preferredY, double x1, double x2,
        IReadOnlyList<BoxRect> obstacles,
        IReadOnlyList<double> reserved,
        int searchDir)
    {
        if (searchDir == 0)
            searchDir = 1;

        for (var i = 0; i < 24; i++)
        {
            var y = preferredY + searchDir * i * LaneSpacing;
            if (HorizontalSegmentBlocked(y, x1, x2, obstacles))
                continue;
            if (IsLaneReserved(y, reserved))
                continue;
            return y;
        }

        for (var i = 1; i < 24; i++)
        {
            var y = preferredY - searchDir * i * LaneSpacing;
            if (HorizontalSegmentBlocked(y, x1, x2, obstacles))
                continue;
            if (IsLaneReserved(y, reserved))
                continue;
            return y;
        }

        return preferredY;
    }

    /// <summary>
    /// Returns whether a vertical segment at <paramref name="x"/> intersects any obstacle rectangle, expanded by <paramref name="clearance"/>.
    /// </summary>
    private static bool VerticalSegmentBlocked(
        double x, double y1, double y2, IReadOnlyList<BoxRect> obstacles, double clearance = BoxClearance)
    {
        var top = Math.Min(y1, y2);
        var bottom = Math.Max(y1, y2);
        foreach (var box in obstacles)
        {
            if (x < box.Left - clearance || x > box.Right + clearance)
                continue;
            if (bottom < box.Top - clearance || top > box.Bottom + clearance)
                continue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether a horizontal segment at <paramref name="y"/> intersects any obstacle rectangle, expanded by <paramref name="clearance"/>.
    /// </summary>
    private static bool HorizontalSegmentBlocked(
        double y, double x1, double x2, IReadOnlyList<BoxRect> obstacles, double clearance = BoxClearance)
    {
        var left = Math.Min(x1, x2);
        var right = Math.Max(x1, x2);
        foreach (var box in obstacles)
        {
            if (y < box.Top - clearance || y > box.Bottom + clearance)
                continue;
            if (right < box.Left - clearance || left > box.Right + clearance)
                continue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Builds a sampled cubic Bezier polyline for two horizontally oriented, roughly aligned pins.
    /// </summary>
    private static List<Point> HorizontalBezier(
        double x1, double y1, PinSide sourceSide,
        double x2, double y2, PinSide targetSide,
        int routeIndex)
    {
        var srcDir = SignOf(sourceSide);
        var tgtDir = SignOf(targetSide);
        var horizontalOffset = Math.Abs(x2 - x1) * 0.5;

        var alignedY = Math.Abs(y2 - y1) < 0.5;
        var laneOffset = alignedY ? GetForwardLaneOffset(routeIndex) : 0;

        // For a horizontal route between vertically-offset pins, blend the control
        // points' Y toward the opposite endpoint so the curve takes a smooth
        // diagonal path instead of bowing vertically.
        var blend = alignedY ? 0 : 0.5;
        var p1y = y1 + (y2 - y1) * blend;
        var p2y = y2 + (y1 - y2) * blend;

        var p0 = new Point(x1, y1);
        var p1 = new Point(x1 + srcDir * horizontalOffset, p1y + laneOffset);
        var p2 = new Point(x2 + tgtDir * horizontalOffset, p2y + laneOffset);
        var p3 = new Point(x2, y2);

        return SampleCubicBezier(p0, p1, p2, p3);
    }

    /// <summary>
    /// Builds a sampled cubic Bezier polyline for two vertically oriented, roughly aligned pins.
    /// </summary>
    private static List<Point> VerticalBezier(
        double x1, double y1, PinSide sourceSide,
        double x2, double y2, PinSide targetSide,
        int routeIndex)
    {
        var srcDir = SignOf(sourceSide);
        var tgtDir = SignOf(targetSide);
        var verticalOffset = Math.Abs(y2 - y1) * 0.5;

        var alignedX = Math.Abs(x2 - x1) < 0.5;
        var laneOffset = alignedX ? GetForwardLaneOffset(routeIndex) : 0;

        // For a vertical route between horizontally-offset pins, blend the control
        // points' X toward the opposite endpoint so the curve takes a smooth
        // diagonal path instead of bowing sideways.
        var blend = alignedX ? 0 : 0.5;
        var p1x = x1 + (x2 - x1) * blend;
        var p2x = x2 + (x1 - x2) * blend;

        var p0 = new Point(x1, y1);
        var p1 = new Point(p1x + laneOffset, y1 + srcDir * verticalOffset);
        var p2 = new Point(p2x + laneOffset, y2 + tgtDir * verticalOffset);
        var p3 = new Point(x2, y2);

        return SampleCubicBezier(p0, p1, p2, p3);
    }

    /// <summary>
    /// Approximates a cubic Bezier curve as a polyline with <see cref="BezierSampleCount"/> segments.
    /// </summary>
    private static List<Point> SampleCubicBezier(Point p0, Point p1, Point p2, Point p3)
    {
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

    /// <summary>
    /// Creates an open <see cref="StreamGeometry"/> polyline through the given points.
    /// </summary>
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

    /// <summary>
    /// Interpolates position and segment tangent at normalized arc length <paramref name="t"/> along a polyline.
    /// </summary>
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

    /// <summary>
    /// Returns the shortest distance from point (<paramref name="px"/>, <paramref name="py"/>) to line segment <paramref name="a"/>–<paramref name="b"/>.
    /// </summary>
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

    /// <summary>
    /// Returns a small perpendicular offset used to stagger overlapping forward Bezier lanes by <paramref name="routeIndex"/>.
    /// </summary>
    private static double GetForwardLaneOffset(int routeIndex)
    {
        if (routeIndex == 0)
            return 0;

        var lane = (routeIndex + 1) / 2;
        return (routeIndex % 2 == 0 ? lane : -lane) * 12;
    }
}

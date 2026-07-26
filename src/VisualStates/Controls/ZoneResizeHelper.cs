using Avalonia;
using Avalonia.Input;
using VisualStates.ViewModels;

namespace VisualStates.Controls;

/// <summary>
/// Identifies which resize handle or drag mode applies at a pointer position on a zone.
/// </summary>
internal enum ZoneResizeEdge
{
    /// <summary>No resize handle; pointer is outside the interactive region.</summary>
    None,

    /// <summary>Pointer is in the zone body; dragging moves the entire zone.</summary>
    Move,

    /// <summary>Top edge resize handle.</summary>
    North,

    /// <summary>Bottom edge resize handle.</summary>
    South,

    /// <summary>Right edge resize handle.</summary>
    East,

    /// <summary>Left edge resize handle.</summary>
    West,

    /// <summary>Top-right corner resize handle.</summary>
    NorthEast,

    /// <summary>Top-left corner resize handle.</summary>
    NorthWest,

    /// <summary>Bottom-right corner resize handle.</summary>
    SouthEast,

    /// <summary>Bottom-left corner resize handle.</summary>
    SouthWest
}

/// <summary>
/// Hit-testing, geometry updates, and cursor selection for resizing and moving zones
/// on the graph canvas.
/// </summary>
internal static class ZoneResizeHelper
{
    /// <summary>
    /// Hit-test tolerance in screen pixels; converted to graph space via <see cref="GraphSlop"/>.
    /// </summary>
    public const double HitSlopScreen = 10.0;

    /// <summary>
    /// Converts screen-space hit slop to graph-space slop for the current zoom level.
    /// </summary>
    /// <param name="zoom">Current viewport zoom factor.</param>
    /// <returns>Hit slop distance in graph coordinates.</returns>
    public static double GraphSlop(double zoom) => HitSlopScreen / Math.Max(zoom, 0.1);

    /// <summary>
    /// Determines which resize edge or move mode applies at a graph-space pointer position.
    /// </summary>
    /// <param name="bodyRect">The zone body rectangle in graph coordinates (excluding title).</param>
    /// <param name="graphPoint">Pointer position in graph coordinates.</param>
    /// <param name="zoom">Current viewport zoom factor.</param>
    /// <returns>
    /// A <see cref="ZoneResizeEdge"/> value: a corner or edge handle, or
    /// <see cref="ZoneResizeEdge.Move"/> when inside the body or outer slop band.
    /// </returns>
    public static ZoneResizeEdge HitTestBody(Rect bodyRect, Point graphPoint, double zoom)
    {
        var slop = GraphSlop(zoom);
        var corner = slop * 1.35;
        var outer = bodyRect.Inflate(slop);
        if (!outer.Contains(graphPoint))
            return ZoneResizeEdge.Move;

        var inner = bodyRect.Deflate(slop);
        if (inner.Width > 8 && inner.Height > 8 && inner.Contains(graphPoint))
            return ZoneResizeEdge.Move;

        var x = graphPoint.X;
        var y = graphPoint.Y;
        var nearLeft = x <= bodyRect.Left + corner;
        var nearRight = x >= bodyRect.Right - corner;
        var nearTop = y <= bodyRect.Top + corner;
        var nearBottom = y >= bodyRect.Bottom - corner;

        if (nearTop && nearLeft)
            return ZoneResizeEdge.NorthWest;
        if (nearTop && nearRight)
            return ZoneResizeEdge.NorthEast;
        if (nearBottom && nearLeft)
            return ZoneResizeEdge.SouthWest;
        if (nearBottom && nearRight)
            return ZoneResizeEdge.SouthEast;
        if (nearTop)
            return ZoneResizeEdge.North;
        if (nearBottom)
            return ZoneResizeEdge.South;
        if (nearLeft)
            return ZoneResizeEdge.West;
        if (nearRight)
            return ZoneResizeEdge.East;

        return ZoneResizeEdge.Move;
    }

    /// <summary>
    /// Applies a resize drag delta to zone origin and size, respecting minimum dimensions.
    /// </summary>
    /// <param name="originX">Zone origin X before the drag (includes title area).</param>
    /// <param name="originY">Zone origin Y before the drag (includes title area).</param>
    /// <param name="originWidth">Zone width before the drag.</param>
    /// <param name="originHeight">Zone body height before the drag (excluding title).</param>
    /// <param name="edge">Active resize edge or <see cref="ZoneResizeEdge.Move"/>.</param>
    /// <param name="deltaX">Horizontal drag delta in graph coordinates.</param>
    /// <param name="deltaY">Vertical drag delta in graph coordinates.</param>
    /// <returns>
    /// Updated zone bounds as <c>(X, Y, Width, Height)</c>, with Y including the title offset.
    /// </returns>
    public static (double X, double Y, double Width, double Height) Apply(
        double originX,
        double originY,
        double originWidth,
        double originHeight,
        ZoneResizeEdge edge,
        double deltaX,
        double deltaY)
    {
        var bodyLeft = originX;
        var bodyTop = originY + ZoneLayout.TitleHeight + ZoneLayout.TitleGap;
        var bodyRight = originX + originWidth;
        var bodyBottom = bodyTop + originHeight;

        switch (edge)
        {
            case ZoneResizeEdge.NorthWest:
                bodyLeft += deltaX;
                bodyTop += deltaY;
                break;
            case ZoneResizeEdge.North:
                bodyTop += deltaY;
                break;
            case ZoneResizeEdge.NorthEast:
                bodyRight += deltaX;
                bodyTop += deltaY;
                break;
            case ZoneResizeEdge.East:
                bodyRight += deltaX;
                break;
            case ZoneResizeEdge.SouthEast:
                bodyRight += deltaX;
                bodyBottom += deltaY;
                break;
            case ZoneResizeEdge.South:
                bodyBottom += deltaY;
                break;
            case ZoneResizeEdge.SouthWest:
                bodyLeft += deltaX;
                bodyBottom += deltaY;
                break;
            case ZoneResizeEdge.West:
                bodyLeft += deltaX;
                break;
        }

        ClampBody(edge, ref bodyLeft, ref bodyTop, ref bodyRight, ref bodyBottom);

        var width = bodyRight - bodyLeft;
        var height = bodyBottom - bodyTop;
        var x = bodyLeft;
        var y = bodyTop - ZoneLayout.TitleHeight - ZoneLayout.TitleGap;
        return (x, y, width, height);
    }

    /// <summary>
    /// Enforces <see cref="ZoneLayout.MinWidth"/> and <see cref="ZoneLayout.MinHeight"/>
    /// on the body rectangle, anchoring the opposite edge according to the active handle.
    /// </summary>
    /// <param name="edge">Active resize edge determining which corner stays fixed.</param>
    /// <param name="bodyLeft">Body left edge; updated in place.</param>
    /// <param name="bodyTop">Body top edge; updated in place.</param>
    /// <param name="bodyRight">Body right edge; updated in place.</param>
    /// <param name="bodyBottom">Body bottom edge; updated in place.</param>
    private static void ClampBody(
        ZoneResizeEdge edge,
        ref double bodyLeft,
        ref double bodyTop,
        ref double bodyRight,
        ref double bodyBottom)
    {
        if (bodyRight - bodyLeft < ZoneLayout.MinWidth)
        {
            if (edge is ZoneResizeEdge.NorthWest or ZoneResizeEdge.West or ZoneResizeEdge.SouthWest)
                bodyRight = bodyLeft + ZoneLayout.MinWidth;
            else if (edge is ZoneResizeEdge.NorthEast or ZoneResizeEdge.East or ZoneResizeEdge.SouthEast)
                bodyLeft = bodyRight - ZoneLayout.MinWidth;
            else
                bodyRight = bodyLeft + ZoneLayout.MinWidth;
        }

        if (bodyBottom - bodyTop < ZoneLayout.MinHeight)
        {
            if (edge is ZoneResizeEdge.NorthWest or ZoneResizeEdge.North or ZoneResizeEdge.NorthEast)
                bodyTop = bodyBottom - ZoneLayout.MinHeight;
            else if (edge is ZoneResizeEdge.SouthWest or ZoneResizeEdge.South or ZoneResizeEdge.SouthEast)
                bodyBottom = bodyTop + ZoneLayout.MinHeight;
            else
                bodyBottom = bodyTop + ZoneLayout.MinHeight;
        }
    }

    /// <summary>
    /// Returns the standard cursor shape for the given resize edge or move mode.
    /// </summary>
    /// <param name="edge">Active resize edge or move mode.</param>
    /// <returns>The appropriate <see cref="StandardCursorType"/> for user feedback.</returns>
    public static StandardCursorType GetCursor(ZoneResizeEdge edge) => edge switch
    {
        ZoneResizeEdge.North => StandardCursorType.TopSide,
        ZoneResizeEdge.South => StandardCursorType.BottomSide,
        ZoneResizeEdge.East => StandardCursorType.RightSide,
        ZoneResizeEdge.West => StandardCursorType.LeftSide,
        ZoneResizeEdge.NorthWest => StandardCursorType.TopLeftCorner,
        ZoneResizeEdge.NorthEast => StandardCursorType.TopRightCorner,
        ZoneResizeEdge.SouthWest => StandardCursorType.BottomLeftCorner,
        ZoneResizeEdge.SouthEast => StandardCursorType.BottomRightCorner,
        ZoneResizeEdge.Move => StandardCursorType.SizeAll,
        _ => StandardCursorType.Arrow
    };
}

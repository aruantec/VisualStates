using Avalonia;
using Avalonia.Input;
using VisualStates.ViewModels;

namespace VisualStates.Controls;

internal enum ZoneResizeEdge
{
    None,
    Move,
    North,
    South,
    East,
    West,
    NorthEast,
    NorthWest,
    SouthEast,
    SouthWest
}

internal static class ZoneResizeHelper
{
    public const double HitSlopScreen = 10.0;

    public static double GraphSlop(double zoom) => HitSlopScreen / Math.Max(zoom, 0.1);

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

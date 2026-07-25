using VisualStates.Core.Models;

namespace VisualStates.Core;

/// <summary>
/// Visual enter/exit flow for zones: children ordered top-to-bottom then left-to-right.
/// Left/Top pins enter the first child; Right/Bottom pins leave the last child.
/// </summary>
public static class ZoneFlow
{
    public static IReadOnlyList<StateBox> GetOrderedChildren(StateProject project, string zoneId) =>
        project.Boxes
            .Where(box => box.ZoneId == zoneId)
            .OrderBy(box => box.Y)
            .ThenBy(box => box.X)
            .ToList();

    public static (string BoxId, string? StepId)? ResolveEnter(StateProject project, string zoneId)
    {
        var children = GetOrderedChildren(project, zoneId);
        if (children.Count == 0)
            return null;

        var first = children[0];
        return (first.Id, first.Steps.FirstOrDefault()?.Id);
    }

    public static (string BoxId, string? StepId)? ResolveExit(StateProject project, string zoneId)
    {
        var children = GetOrderedChildren(project, zoneId);
        if (children.Count == 0)
            return null;

        var last = children[^1];
        return (last.Id, last.Steps.Count > 0 ? last.Steps[^1].Id : null);
    }

    public static bool IsInputSide(PinSide side) => side is PinSide.Left or PinSide.Top;

    public static bool IsOutputSide(PinSide side) =>
        side is PinSide.Right or PinSide.Bottom or PinSide.Error;
}

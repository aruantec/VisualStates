using VisualStates.Core.Models;

namespace VisualStates.Core;

/// <summary>
/// Visual enter/exit flow for zones: children ordered top-to-bottom then left-to-right.
/// The first child is the entry point; the last is the exit point.
/// Pins themselves are direction-agnostic — the user picks source and target by drag.
/// </summary>
public static class ZoneFlow
{
    /// <summary>
    /// Returns the boxes belonging to <paramref name="zoneId"/> in visual reading order:
    /// top-to-bottom, then left-to-right.
    /// </summary>
    /// <param name="project">Project that owns the boxes.</param>
    /// <param name="zoneId">Zone whose children to order.</param>
    public static IReadOnlyList<StateBox> GetOrderedChildren(StateProject project, string zoneId) =>
        project.Boxes
            .Where(box => box.ZoneId == zoneId)
            .OrderBy(box => box.Y)
            .ThenBy(box => box.X)
            .ToList();

    /// <summary>
    /// Resolves the enter endpoint for a zone: the first step of the first child box,
    /// or the empty box itself when it has no steps.
    /// </summary>
    /// <param name="project">Project that owns the zone.</param>
    /// <param name="zoneId">Zone to enter.</param>
    /// <returns>
    /// A (boxId, stepId) pair, or <see langword="null"/> when the zone has no children.
    /// </returns>
    public static (string BoxId, string? StepId)? ResolveEnter(StateProject project, string zoneId)
    {
        var children = GetOrderedChildren(project, zoneId);
        if (children.Count == 0)
            return null;

        var first = children[0];
        return (first.Id, first.Steps.FirstOrDefault()?.Id);
    }

    /// <summary>
    /// Resolves the exit endpoint for a zone: the last step of the last child box,
    /// or the empty box itself when it has no steps.
    /// </summary>
    /// <param name="project">Project that owns the zone.</param>
    /// <param name="zoneId">Zone to leave.</param>
    /// <returns>
    /// A (boxId, stepId) pair, or <see langword="null"/> when the zone has no children.
    /// </returns>
    public static (string BoxId, string? StepId)? ResolveExit(StateProject project, string zoneId)
    {
        var children = GetOrderedChildren(project, zoneId);
        if (children.Count == 0)
            return null;

        var last = children[^1];
        return (last.Id, last.Steps.Count > 0 ? last.Steps[^1].Id : null);
    }
}

namespace VisualStates.Core.Models;

/// <summary>
/// Root document for a VisualStates project: zones, boxes, connections, variables,
/// and code-generation settings. Serialized to/from <c>.state</c> JSON files.
/// </summary>
public sealed class StateProject
{
    /// <summary>Schema version of the project file format.</summary>
    public int Version { get; set; } = 2;

    /// <summary>Human-readable project name.</summary>
    public string Name { get; set; } = "Untitled";

    /// <summary>Name of the C# class emitted by the code generator.</summary>
    public string GeneratedClassName { get; set; } = "GeneratedStateMachine";

    /// <summary>Namespace of the generated C# class.</summary>
    public string Namespace { get; set; } = "VisualStates.Generated";

    /// <summary>Zones that group related state boxes.</summary>
    public List<Zone> Zones { get; set; } = [];

    /// <summary>State boxes on the graph.</summary>
    public List<StateBox> Boxes { get; set; } = [];

    /// <summary>Directed connections between pins.</summary>
    public List<StateConnection> Connections { get; set; } = [];

    /// <summary>Project-level variables exposed on the generated class.</summary>
    public List<StateVariable> Variables { get; set; } = [];

    /// <summary>Finds a zone by id, or returns null if not found.</summary>
    /// <param name="id">Zone id to look up.</param>
    public Zone? FindZone(string id) => Zones.FirstOrDefault(z => z.Id == id);

    /// <summary>Finds a box by id, or returns null if not found.</summary>
    /// <param name="id">Box id to look up.</param>
    public StateBox? FindBox(string id) => Boxes.FirstOrDefault(b => b.Id == id);

    /// <summary>
    /// Finds a step by box id and step id, or returns null if either is missing.
    /// </summary>
    /// <param name="boxId">Parent box id.</param>
    /// <param name="stepId">Step id within that box.</param>
    public StateStep? FindStep(string boxId, string stepId) =>
        FindBox(boxId)?.Steps.FirstOrDefault(s => s.Id == stepId);
}

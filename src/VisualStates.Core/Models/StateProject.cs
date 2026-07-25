namespace VisualStates.Core.Models;

public sealed class StateProject
{
    public int Version { get; set; } = 2;
    public string Name { get; set; } = "Untitled";
    public string GeneratedClassName { get; set; } = "GeneratedStateMachine";
    public string Namespace { get; set; } = "VisualStates.Generated";
    public List<Zone> Zones { get; set; } = [];
    public List<StateBox> Boxes { get; set; } = [];
    public List<StateConnection> Connections { get; set; } = [];
    public List<StateVariable> Variables { get; set; } = [];

    public Zone? FindZone(string id) => Zones.FirstOrDefault(z => z.Id == id);
    public StateBox? FindBox(string id) => Boxes.FirstOrDefault(b => b.Id == id);
    public StateStep? FindStep(string boxId, string stepId) =>
        FindBox(boxId)?.Steps.FirstOrDefault(s => s.Id == stepId);
}

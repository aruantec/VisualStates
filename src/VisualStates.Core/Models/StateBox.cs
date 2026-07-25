namespace VisualStates.Core.Models;

public sealed class StateBox
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "State";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 220;
    public bool IsEntry { get; set; }
    public string? HeaderColor { get; set; }
    public string? ZoneId { get; set; }
    public List<StateStep> Steps { get; set; } = [];
}

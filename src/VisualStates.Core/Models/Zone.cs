namespace VisualStates.Core.Models;

public sealed class Zone
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Zone";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 360;
    public double Height { get; set; } = 280;
    public string? BorderColor { get; set; }
}

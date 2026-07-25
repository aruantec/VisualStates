namespace VisualStates.Core.Models;

public sealed class StateVariable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Variable";
    public string TypeName { get; set; } = "string";
    public string? DefaultValue { get; set; }
}

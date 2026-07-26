namespace VisualStates.Core.Models;

/// <summary>
/// A project-level variable emitted as a property on the generated state machine class.
/// </summary>
public sealed class StateVariable
{
    /// <summary>Unique identifier for this variable.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Identifier used as the generated C# property name.</summary>
    public string Name { get; set; } = "Variable";

    /// <summary>C# type name of the property (e.g. <c>string</c>, <c>int</c>).</summary>
    public string TypeName { get; set; } = "string";

    /// <summary>
    /// Optional initializer expression written into the generated property.
    /// When null, the property is left uninitialized.
    /// </summary>
    public string? DefaultValue { get; set; }
}

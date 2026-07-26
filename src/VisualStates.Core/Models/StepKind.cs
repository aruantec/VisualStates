namespace VisualStates.Core.Models;

/// <summary>
/// Discriminator for the action a <see cref="StateStep"/> performs when executed.
/// </summary>
public enum StepKind
{
    /// <summary>Assigns an expression result to a named variable.</summary>
    SetVariable,

    /// <summary>Raises a named event through the runtime context.</summary>
    CallEvent,

    /// <summary>Invokes a named method through the runtime context.</summary>
    CallMethod
}

namespace VisualStates.Core.Models;

/// <summary>
/// A single executable action inside a <see cref="StateBox"/>.
/// The fields that apply depend on <see cref="Kind"/>.
/// </summary>
public sealed class StateStep
{
    /// <summary>Unique identifier for this step.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Display name for the step.</summary>
    public string Name { get; set; } = "Step";

    /// <summary>Action kind that determines how the step is executed and generated.</summary>
    public StepKind Kind { get; set; } = StepKind.SetVariable;

    /// <summary>
    /// Generic target name used as a fallback when the kind-specific name
    /// (<see cref="MethodName"/> / <see cref="EventName"/>) is empty.
    /// For <see cref="StepKind.SetVariable"/> this is the variable name.
    /// </summary>
    public string? TargetName { get; set; }

    /// <summary>
    /// Expression assigned to the target variable when
    /// <see cref="Kind"/> is <see cref="StepKind.SetVariable"/>.
    /// </summary>
    public string? Expression { get; set; }

    /// <summary>
    /// Method name invoked when <see cref="Kind"/> is <see cref="StepKind.CallMethod"/>.
    /// </summary>
    public string? MethodName { get; set; }

    /// <summary>
    /// Event name raised when <see cref="Kind"/> is <see cref="StepKind.CallEvent"/>.
    /// </summary>
    public string? EventName { get; set; }

    /// <summary>
    /// Optional comma-separated argument list for
    /// <see cref="StepKind.CallMethod"/> invocations.
    /// </summary>
    public string? Arguments { get; set; }
}

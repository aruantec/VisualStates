using CommunityToolkit.Mvvm.ComponentModel;
using VisualStates.Core.Models;

namespace VisualStates.ViewModels;

/// <summary>
/// View-model wrapper around a <see cref="StateStep"/> that keeps the model and
/// UI bindings in sync.
/// </summary>
public partial class StateStepViewModel : ViewModelBase
{
    /// <summary>
    /// Creates a view-model for <paramref name="model"/> owned by
    /// <paramref name="parent"/>.
    /// </summary>
    /// <param name="model">Underlying step model.</param>
    /// <param name="parent">Parent box view-model.</param>
    public StateStepViewModel(StateStep model, StateBoxViewModel parent)
    {
        Model = model;
        Parent = parent;
    }

    /// <summary>Underlying domain model.</summary>
    public StateStep Model { get; }

    /// <summary>Parent box that owns this step.</summary>
    public StateBoxViewModel Parent { get; }

    /// <summary>Step id (from the model).</summary>
    public string Id => Model.Id;

    /// <summary>Display name of the step.</summary>
    public string Name
    {
        get => Model.Name;
        set
        {
            if (Model.Name == value)
                return;

            Model.Name = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Action kind of the step.</summary>
    public StepKind Kind
    {
        get => Model.Kind;
        set
        {
            if (Model.Kind == value)
                return;

            Model.Kind = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(KindLabel));
            OnPropertyChanged(nameof(AccentBrushKey));
        }
    }

    /// <summary>Generic target / variable name.</summary>
    public string? TargetName
    {
        get => Model.TargetName;
        set
        {
            if (Model.TargetName == value)
                return;

            Model.TargetName = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Expression for <see cref="StepKind.SetVariable"/> steps.</summary>
    public string? Expression
    {
        get => Model.Expression;
        set
        {
            if (Model.Expression == value)
                return;

            Model.Expression = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Method name for <see cref="StepKind.CallMethod"/> steps.</summary>
    public string? MethodName
    {
        get => Model.MethodName;
        set
        {
            if (Model.MethodName == value)
                return;

            Model.MethodName = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Event name for <see cref="StepKind.CallEvent"/> steps.</summary>
    public string? EventName
    {
        get => Model.EventName;
        set
        {
            if (Model.EventName == value)
                return;

            Model.EventName = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Human-readable label for <see cref="Kind"/>.</summary>
    public string KindLabel => Kind switch
    {
        StepKind.SetVariable => "Set Variable",
        StepKind.CallEvent => "Call Event",
        StepKind.CallMethod => "Call Method",
        _ => Kind.ToString()
    };

    /// <summary>Resource key used to pick an accent brush for this kind.</summary>
    public string AccentBrushKey => Kind switch
    {
        StepKind.SetVariable => "StepSetVariable",
        StepKind.CallEvent => "StepCallEvent",
        StepKind.CallMethod => "StepCallMethod",
        _ => "StepDefault"
    };
}

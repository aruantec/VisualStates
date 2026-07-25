using CommunityToolkit.Mvvm.ComponentModel;
using VisualStates.Core.Models;

namespace VisualStates.ViewModels;

public partial class StateStepViewModel : ViewModelBase
{
    public StateStepViewModel(StateStep model, StateBoxViewModel parent)
    {
        Model = model;
        Parent = parent;
    }

    public StateStep Model { get; }
    public StateBoxViewModel Parent { get; }

    public string Id => Model.Id;

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

    public string KindLabel => Kind switch
    {
        StepKind.SetVariable => "Set Variable",
        StepKind.CallEvent => "Call Event",
        StepKind.CallMethod => "Call Method",
        _ => Kind.ToString()
    };

    public string AccentBrushKey => Kind switch
    {
        StepKind.SetVariable => "StepSetVariable",
        StepKind.CallEvent => "StepCallEvent",
        StepKind.CallMethod => "StepCallMethod",
        _ => "StepDefault"
    };
}

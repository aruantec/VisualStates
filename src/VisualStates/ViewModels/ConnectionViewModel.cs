using CommunityToolkit.Mvvm.ComponentModel;
using VisualStates.Core.Models;

namespace VisualStates.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    public ConnectionViewModel(StateConnection model, MainViewModel main)
    {
        Model = model;
        Main = main;
    }

    public StateConnection Model { get; }
    public MainViewModel Main { get; }

    public string Id => Model.Id;

    public StateBoxViewModel? SourceBox => Main.FindBox(Model.SourceBoxId);
    public StateBoxViewModel? TargetBox => Main.FindBox(Model.TargetBoxId);
    public StateStepViewModel? SourceStep => Main.FindStep(Model.SourceBoxId, Model.SourceStepId);
    public StateStepViewModel? TargetStep => Main.FindStep(Model.TargetBoxId, Model.TargetStepId);

    [ObservableProperty]
    private bool _isSelected;

    public (double X, double Y) GetSourcePoint()
    {
        var source = SourceBox;
        if (source is null)
            return (0, 0);

        return SourceStep is not null
            ? source.GetStepPinPosition(SourceStep, isOutput: true)
            : source.GetBoxPinPosition(isOutput: true);
    }

    public (double X, double Y) GetTargetPoint()
    {
        var target = TargetBox;
        if (target is null)
            return (0, 0);

        return TargetStep is not null
            ? target.GetStepPinPosition(TargetStep, isOutput: false)
            : target.GetBoxPinPosition(isOutput: false);
    }
}

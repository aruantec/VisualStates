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
    public ZoneViewModel? SourceZone =>
        string.IsNullOrWhiteSpace(Model.SourceZoneId) ? null : Main.FindZone(Model.SourceZoneId);
    public ZoneViewModel? TargetZone =>
        string.IsNullOrWhiteSpace(Model.TargetZoneId) ? null : Main.FindZone(Model.TargetZoneId);
    public StateStepViewModel? SourceStep => Main.FindStep(Model.SourceBoxId, Model.SourceStepId);
    public StateStepViewModel? TargetStep => Main.FindStep(Model.TargetBoxId, Model.TargetStepId);

    public PinSide SourceSide => Model.SourceSide;
    public PinSide TargetSide => Model.TargetSide;

    [ObservableProperty]
    private bool _isSelected;

    public (double X, double Y) GetSourcePoint()
    {
        if (SourceZone is not null)
            return SourceZone.GetPinPosition(SourceSide);

        var source = SourceBox;
        if (source is null)
            return (0, 0);

        return SourceStep is not null
            ? source.GetStepPinPosition(SourceStep, SourceSide)
            : source.GetBoxPinPosition(SourceSide);
    }

    public (double X, double Y) GetTargetPoint()
    {
        if (TargetZone is not null)
            return TargetZone.GetPinPosition(TargetSide);

        var target = TargetBox;
        if (target is null)
            return (0, 0);

        return TargetStep is not null
            ? target.GetStepPinPosition(TargetStep, TargetSide)
            : target.GetBoxPinPosition(TargetSide);
    }
}

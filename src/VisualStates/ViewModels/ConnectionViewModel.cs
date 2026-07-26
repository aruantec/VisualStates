using CommunityToolkit.Mvvm.ComponentModel;
using VisualStates.Core.Models;

namespace VisualStates.ViewModels;

/// <summary>
/// View-model for a directed wire between two pins. Resolves endpoint boxes,
/// steps, and zones through the owning <see cref="MainViewModel"/>.
/// </summary>
public partial class ConnectionViewModel : ViewModelBase
{
    /// <summary>
    /// Creates a connection view-model bound to <paramref name="model"/> and
    /// <paramref name="main"/>.
    /// </summary>
    /// <param name="model">Underlying connection model.</param>
    /// <param name="main">Root editor view-model used for lookups.</param>
    public ConnectionViewModel(StateConnection model, MainViewModel main)
    {
        Model = model;
        Main = main;
    }

    /// <summary>Underlying domain model.</summary>
    public StateConnection Model { get; }

    /// <summary>Owning editor view-model.</summary>
    public MainViewModel Main { get; }

    /// <summary>Connection id.</summary>
    public string Id => Model.Id;

    /// <summary>Resolved source box, or null when missing / zone-only.</summary>
    public StateBoxViewModel? SourceBox => Main.FindBox(Model.SourceBoxId);

    /// <summary>Resolved target box, or null when missing / zone-only.</summary>
    public StateBoxViewModel? TargetBox => Main.FindBox(Model.TargetBoxId);

    /// <summary>Resolved source zone when <see cref="StateConnection.SourceZoneId"/> is set.</summary>
    public ZoneViewModel? SourceZone =>
        string.IsNullOrWhiteSpace(Model.SourceZoneId) ? null : Main.FindZone(Model.SourceZoneId);

    /// <summary>Resolved target zone when <see cref="StateConnection.TargetZoneId"/> is set.</summary>
    public ZoneViewModel? TargetZone =>
        string.IsNullOrWhiteSpace(Model.TargetZoneId) ? null : Main.FindZone(Model.TargetZoneId);

    /// <summary>Resolved source step within the source box.</summary>
    public StateStepViewModel? SourceStep => Main.FindStep(Model.SourceBoxId, Model.SourceStepId);

    /// <summary>Resolved target step within the target box.</summary>
    public StateStepViewModel? TargetStep => Main.FindStep(Model.TargetBoxId, Model.TargetStepId);

    /// <summary>Geometric side of the source pin.</summary>
    public PinSide SourceSide => Model.SourceSide;

    /// <summary>Geometric side of the target pin.</summary>
    public PinSide TargetSide => Model.TargetSide;

    /// <summary>
    /// True when this wire is an error/exit branch (explicit flag or Error pin).
    /// </summary>
    public bool IsError => Model.IsError || Model.SourceSide == PinSide.Error;

    /// <summary>Whether this connection is currently selected in the editor.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Graph-space position of the source pin (zone pin, step pin, or box pin).
    /// </summary>
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

    /// <summary>
    /// Graph-space position of the target pin (zone pin, step pin, or box pin).
    /// </summary>
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

using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using VisualStates.Core;
using VisualStates.Core.Models;

namespace VisualStates.ViewModels;

/// <summary>
/// View-model for a state box on the graph canvas. Exposes layout, styling,
/// child steps, and pin positions for connection routing.
/// </summary>
public partial class StateBoxViewModel : ViewModelBase
{
    /// <summary>
    /// Creates a box view-model bound to <paramref name="model"/> and
    /// <paramref name="main"/>, wrapping each step in a
    /// <see cref="StateStepViewModel"/>.
    /// </summary>
    /// <param name="model">Underlying box model.</param>
    /// <param name="main">Root editor view-model used for graph notifications and lookups.</param>
    public StateBoxViewModel(StateBox model, MainViewModel main)
    {
        Model = model;
        Main = main;
        EnsureHeaderColor();
        Steps = new ObservableCollection<StateStepViewModel>(
            model.Steps.Select(step => new StateStepViewModel(step, this)));
    }

    /// <summary>Underlying domain model.</summary>
    public StateBox Model { get; }

    /// <summary>Owning editor view-model.</summary>
    public MainViewModel Main { get; }

    /// <summary>Ordered collection of step view-models for this box.</summary>
    public ObservableCollection<StateStepViewModel> Steps { get; }

    /// <summary>Box id (from the model).</summary>
    public string Id => Model.Id;

    /// <summary>Display name of the box.</summary>
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

    /// <summary>Canvas X coordinate of the box origin.</summary>
    public double X
    {
        get => Model.X;
        set
        {
            if (Math.Abs(Model.X - value) < 0.01)
                return;

            Model.X = value;
            OnPropertyChanged();
            Main.NotifyGraphChanged();
        }
    }

    /// <summary>Canvas Y coordinate of the box origin.</summary>
    public double Y
    {
        get => Model.Y;
        set
        {
            if (Math.Abs(Model.Y - value) < 0.01)
                return;

            Model.Y = value;
            OnPropertyChanged();
            Main.NotifyGraphChanged();
        }
    }

    /// <summary>Rendered width of the box body.</summary>
    public double Width
    {
        get => Model.Width;
        set
        {
            if (Math.Abs(Model.Width - value) < 0.01)
                return;

            Model.Width = value;
            OnPropertyChanged();
            Main.NotifyGraphChanged();
        }
    }

    /// <summary>
    /// Whether this box is the main entry point of the state graph.
    /// Setting <see langword="true"/> clears the flag on every other box.
    /// </summary>
    public bool IsEntry
    {
        get => Model.IsEntry;
        set
        {
            if (Model.IsEntry == value)
                return;

            if (value)
            {
                Main.SetAsEntryPoint(this);
                return;
            }

            // Keep at least one entry when possible — ignore uncheck on the current main.
            if (Main.Boxes.Count(b => b.IsEntry) <= 1)
                return;

            Model.IsEntry = false;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Updates <see cref="IsEntry"/> without re-entering <see cref="MainViewModel.SetAsEntryPoint"/>.
    /// </summary>
    /// <param name="value">New entry-point flag.</param>
    internal void SetIsEntryCore(bool value)
    {
        if (Model.IsEntry == value)
            return;

        Model.IsEntry = value;
        OnPropertyChanged(nameof(IsEntry));
    }

    /// <summary>
    /// Header accent color as a normalized RGB string. Assigning blank values
    /// picks a stable palette color from <see cref="Id"/>.
    /// </summary>
    public string HeaderColor
    {
        get => BoxColorPalette.Normalize(Model.HeaderColor, Model.Id);
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? BoxColorPalette.PickForId(Model.Id)
                : value.Trim();
            if (string.Equals(Model.HeaderColor, normalized, StringComparison.OrdinalIgnoreCase))
                return;

            Model.HeaderColor = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HeaderBrush));
            Main.NotifyGraphChanged();
        }
    }

    /// <summary>Avalonia brush derived from <see cref="HeaderColor"/>.</summary>
    public IBrush HeaderBrush
    {
        get
        {
            var (r, g, b) = BoxColorPalette.ParseRgb(HeaderColor, Model.Id);
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
    }

    /// <summary>
    /// Ensures the model has a header color, assigning one from the palette
    /// when unset.
    /// </summary>
    public void EnsureHeaderColor()
    {
        if (!string.IsNullOrWhiteSpace(Model.HeaderColor))
            return;

        Model.HeaderColor = BoxColorPalette.PickForId(Model.Id);
        OnPropertyChanged(nameof(HeaderColor));
        OnPropertyChanged(nameof(HeaderBrush));
    }

    /// <summary>Whether this box is currently selected in the editor.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Id of the parent zone that contains this box, or <see langword="null"/>
    /// when the box is ungrouped.
    /// </summary>
    public string? ZoneId
    {
        get => Model.ZoneId;
        set
        {
            if (Model.ZoneId == value)
                return;

            Model.ZoneId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ParentZone));
            Main.NotifyGraphChanged();
        }
    }

    /// <summary>
    /// Resolved parent zone view-model, or <see langword="null"/> when
    /// <see cref="ZoneId"/> is unset or unknown.
    /// </summary>
    public ZoneViewModel? ParentZone =>
        string.IsNullOrWhiteSpace(ZoneId) ? null : Main.FindZone(ZoneId);

    /// <summary>
    /// Appends a new step to the box model and exposes it in
    /// <see cref="Steps"/>.
    /// </summary>
    /// <param name="step">Step model to add.</param>
    public void AddStep(StateStep step)
    {
        Model.Steps.Add(step);
        Steps.Add(new StateStepViewModel(step, this));
        Main.NotifyGraphChanged();
    }

    /// <summary>
    /// Removes a step from the box model and from <see cref="Steps"/>.
    /// </summary>
    /// <param name="step">Step view-model to remove.</param>
    public void RemoveStep(StateStepViewModel step)
    {
        Model.Steps.Remove(step.Model);
        Steps.Remove(step);
        Main.NotifyGraphChanged();
    }

    /// <summary>
    /// Graph-space coordinates of a step connection pin, or the box-level pin
    /// when <paramref name="step"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="step">Target step, or <see langword="null"/> for the box header pin row.</param>
    /// <param name="side">Edge or error pin to locate.</param>
    /// <returns>Pin center in canvas coordinates.</returns>
    public (double X, double Y) GetStepPinPosition(StateStepViewModel? step, PinSide side)
    {
        if (side == PinSide.Error && step is not null)
            return GetStepErrorPinPosition(step);

        const double headerHeight = 34;
        const double stepHeight = 42;
        const double padding = 12;

        var index = step is null ? -1 : Steps.IndexOf(step);
        var rowY = Y + headerHeight + padding + Math.Max(0, index) * stepHeight + stepHeight / 2;
        return PositionForSide(side, rowY);
    }

    /// <summary>
    /// Graph-space coordinates of a box-level connection pin on the header row.
    /// </summary>
    /// <param name="side">Edge or error pin to locate.</param>
    /// <returns>Pin center in canvas coordinates.</returns>
    public (double X, double Y) GetBoxPinPosition(PinSide side)
    {
        if (side == PinSide.Error)
            return GetBoxErrorPinPosition();

        var rowY = Y + 34;
        return PositionForSide(side, rowY);
    }

    private (double X, double Y) PositionForSide(PinSide side, double rowY) =>
        side switch
        {
            PinSide.Left => (X, rowY),
            PinSide.Right => (X + Width, rowY),
            PinSide.Top => (X + Width / 2, Y + 1),
            PinSide.Bottom => (X + Width / 2, Y + GetTotalHeight() - 1),
            _ => (X, rowY)
        };

    /// <summary>
    /// Graph-space coordinates of the dedicated error pin for
    /// <paramref name="step"/> (top-right inset of the step row).
    /// </summary>
    /// <param name="step">Step whose error pin to locate.</param>
    /// <returns>Error pin position in canvas coordinates.</returns>
    public (double X, double Y) GetStepErrorPinPosition(StateStepViewModel step)
    {
        const double headerHeight = 34;
        const double stepHeight = 42;
        const double padding = 12;
        const double stepInset = 10;

        var index = Math.Max(0, Steps.IndexOf(step));
        var stepTop = Y + headerHeight + padding + index * stepHeight;
        return (X + Width - stepInset, stepTop);
    }

    /// <summary>
    /// Graph-space coordinates of the box-level error pin (top-right corner).
    /// </summary>
    /// <returns>Error pin position in canvas coordinates.</returns>
    public (double X, double Y) GetBoxErrorPinPosition() =>
        (X + Width - 2, Y + 2);

    /// <summary>
    /// Total rendered height of the box, including header, steps, and padding.
    /// </summary>
    /// <returns>Height in canvas units.</returns>
    public double GetTotalHeight() =>
        34 + Math.Max(1, Steps.Count) * 42 + 24;
}

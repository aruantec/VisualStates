using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using VisualStates.Core;
using VisualStates.Core.Models;

namespace VisualStates.ViewModels;

public partial class StateBoxViewModel : ViewModelBase
{
    public StateBoxViewModel(StateBox model, MainViewModel main)
    {
        Model = model;
        Main = main;
        EnsureHeaderColor();
        Steps = new ObservableCollection<StateStepViewModel>(
            model.Steps.Select(step => new StateStepViewModel(step, this)));
    }

    public StateBox Model { get; }
    public MainViewModel Main { get; }
    public ObservableCollection<StateStepViewModel> Steps { get; }

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

    public bool IsEntry
    {
        get => Model.IsEntry;
        set
        {
            if (Model.IsEntry == value)
                return;

            Model.IsEntry = value;
            OnPropertyChanged();
        }
    }

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

    public IBrush HeaderBrush
    {
        get
        {
            var (r, g, b) = BoxColorPalette.ParseRgb(HeaderColor, Model.Id);
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
    }

    public void EnsureHeaderColor()
    {
        if (!string.IsNullOrWhiteSpace(Model.HeaderColor))
            return;

        Model.HeaderColor = BoxColorPalette.PickForId(Model.Id);
        OnPropertyChanged(nameof(HeaderColor));
        OnPropertyChanged(nameof(HeaderBrush));
    }

    [ObservableProperty]
    private bool _isSelected;

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

    public ZoneViewModel? ParentZone =>
        string.IsNullOrWhiteSpace(ZoneId) ? null : Main.FindZone(ZoneId);

    public void AddStep(StateStep step)
    {
        Model.Steps.Add(step);
        Steps.Add(new StateStepViewModel(step, this));
        Main.NotifyGraphChanged();
    }

    public void RemoveStep(StateStepViewModel step)
    {
        Model.Steps.Remove(step.Model);
        Steps.Remove(step);
        Main.NotifyGraphChanged();
    }

    public (double X, double Y) GetStepPinPosition(StateStepViewModel? step, bool isOutput)
    {
        const double headerHeight = 34;
        const double stepHeight = 42;
        const double padding = 12;

        var index = step is null ? -1 : Steps.IndexOf(step);
        var y = Y + headerHeight + padding + Math.Max(0, index) * stepHeight + stepHeight / 2;
        var x = isOutput ? X + Width : X;
        return (x, y);
    }

    public (double X, double Y) GetBoxPinPosition(bool isOutput)
    {
        var y = Y + 34;
        var x = isOutput ? X + Width : X;
        return (x, y);
    }

    public double GetTotalHeight() =>
        34 + Math.Max(1, Steps.Count) * 42 + 24;
}

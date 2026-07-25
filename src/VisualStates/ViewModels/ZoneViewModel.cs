using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using VisualStates.Core;
using VisualStates.Core.Models;

namespace VisualStates.ViewModels;

public partial class ZoneViewModel : ViewModelBase
{
    public ZoneViewModel(Zone model, MainViewModel main)
    {
        Model = model;
        Main = main;
        EnsureBorderColor();
    }

    public Zone Model { get; }
    public MainViewModel Main { get; }

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
            Main.NotifyGraphChanged();
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
            OnPropertyChanged(nameof(BodyTop));
            OnPropertyChanged(nameof(BodyRect));
            OnPropertyChanged(nameof(InteractionRect));
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
            OnPropertyChanged(nameof(BodyTop));
            OnPropertyChanged(nameof(BodyRect));
            OnPropertyChanged(nameof(InteractionRect));
            Main.NotifyGraphChanged();
        }
    }

    public double Width
    {
        get => Model.Width;
        set
        {
            var clamped = Math.Max(ZoneLayout.MinWidth, value);
            if (Math.Abs(Model.Width - clamped) < 0.01)
                return;

            Model.Width = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BodyRect));
            OnPropertyChanged(nameof(InteractionRect));
            Main.NotifyGraphChanged();
        }
    }

    public double Height
    {
        get => Model.Height;
        set
        {
            var clamped = Math.Max(ZoneLayout.MinHeight, value);
            if (Math.Abs(Model.Height - clamped) < 0.01)
                return;

            Model.Height = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BodyRect));
            OnPropertyChanged(nameof(InteractionRect));
            Main.NotifyGraphChanged();
        }
    }

    public string BorderColor
    {
        get => BoxColorPalette.Normalize(Model.BorderColor, Model.Id);
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? BoxColorPalette.PickForId(Model.Id)
                : value.Trim();
            if (string.Equals(Model.BorderColor, normalized, StringComparison.OrdinalIgnoreCase))
                return;

            Model.BorderColor = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BorderBrush));
            OnPropertyChanged(nameof(AccentColor));
            Main.NotifyGraphChanged();
        }
    }

    public IBrush BorderBrush
    {
        get
        {
            var (r, g, b) = BoxColorPalette.ParseRgb(BorderColor, Model.Id);
            return new SolidColorBrush(Color.FromArgb(200, r, g, b));
        }
    }

    public Color AccentColor
    {
        get
        {
            var (r, g, b) = BoxColorPalette.ParseRgb(BorderColor, Model.Id);
            return Color.FromRgb(r, g, b);
        }
    }

    [ObservableProperty]
    private bool _isSelected;

    public double BodyTop => Y + ZoneLayout.TitleHeight + ZoneLayout.TitleGap;

    public Rect BodyRect => new(X, BodyTop, Width, Height);

    public Rect InteractionRect => new(X, Y, Width, BodyTop + Height - Y);

    public void EnsureBorderColor()
    {
        if (!string.IsNullOrWhiteSpace(Model.BorderColor))
            return;

        Model.BorderColor = BoxColorPalette.PickForId(Model.Id);
        OnPropertyChanged(nameof(BorderColor));
        OnPropertyChanged(nameof(BorderBrush));
        OnPropertyChanged(nameof(AccentColor));
    }

    public bool ContainsBodyPoint(double x, double y) =>
        x >= BodyRect.X && x <= BodyRect.Right && y >= BodyRect.Y && y <= BodyRect.Bottom;

    public bool ContainsInteractionPoint(double x, double y) =>
        x >= InteractionRect.X && x <= InteractionRect.Right &&
        y >= InteractionRect.Y && y <= InteractionRect.Bottom;

    public IEnumerable<StateBoxViewModel> GetChildBoxes() =>
        Main.Boxes.Where(box => box.ZoneId == Id);
}

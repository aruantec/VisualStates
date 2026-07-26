using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using VisualStates.Core;
using VisualStates.Core.Models;

namespace VisualStates.ViewModels;

/// <summary>
/// View-model for a grouping zone on the graph canvas. Manages layout bounds,
/// border styling, child box membership, and zone-level connection pins.
/// </summary>
public partial class ZoneViewModel : ViewModelBase
{
    /// <summary>
    /// Creates a zone view-model bound to <paramref name="model"/> and
    /// <paramref name="main"/>.
    /// </summary>
    /// <param name="model">Underlying zone model.</param>
    /// <param name="main">Root editor view-model used for graph notifications and lookups.</param>
    public ZoneViewModel(Zone model, MainViewModel main)
    {
        Model = model;
        Main = main;
        EnsureBorderColor();
    }

    /// <summary>Underlying domain model.</summary>
    public Zone Model { get; }

    /// <summary>Owning editor view-model.</summary>
    public MainViewModel Main { get; }

    /// <summary>Zone id (from the model).</summary>
    public string Id => Model.Id;

    /// <summary>Display name shown in the zone title bar.</summary>
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

    /// <summary>Canvas X coordinate of the zone origin.</summary>
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

    /// <summary>Canvas Y coordinate of the zone origin.</summary>
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

    /// <summary>
    /// Width of the zone body; clamped to at least
    /// <see cref="ZoneLayout.MinWidth"/>.
    /// </summary>
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

    /// <summary>
    /// Height of the zone body; clamped to at least
    /// <see cref="ZoneLayout.MinHeight"/>.
    /// </summary>
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

    /// <summary>
    /// Border accent color as a normalized RGB string. Assigning blank values
    /// picks a stable palette color from <see cref="Id"/>.
    /// </summary>
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

    /// <summary>Semi-transparent Avalonia brush derived from <see cref="BorderColor"/>.</summary>
    public IBrush BorderBrush
    {
        get
        {
            var (r, g, b) = BoxColorPalette.ParseRgb(BorderColor, Model.Id);
            return new SolidColorBrush(Color.FromArgb(200, r, g, b));
        }
    }

    /// <summary>Opaque accent color derived from <see cref="BorderColor"/>.</summary>
    public Color AccentColor
    {
        get
        {
            var (r, g, b) = BoxColorPalette.ParseRgb(BorderColor, Model.Id);
            return Color.FromRgb(r, g, b);
        }
    }

    /// <summary>Whether this zone is currently selected in the editor.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Canvas Y coordinate where the zone body begins, below the title bar and gap.
    /// </summary>
    public double BodyTop => Y + ZoneLayout.TitleHeight + ZoneLayout.TitleGap;

    /// <summary>Axis-aligned rectangle of the zone body (excludes the title bar).</summary>
    public Rect BodyRect => new(X, BodyTop, Width, Height);

    /// <summary>
    /// Hit-test rectangle covering the title bar and body, used for selection
    /// and drag interactions.
    /// </summary>
    public Rect InteractionRect => new(X, Y, Width, BodyTop + Height - Y);

    /// <summary>
    /// Ensures the model has a border color, assigning one from the palette
    /// when unset.
    /// </summary>
    public void EnsureBorderColor()
    {
        if (!string.IsNullOrWhiteSpace(Model.BorderColor))
            return;

        Model.BorderColor = BoxColorPalette.PickForId(Model.Id);
        OnPropertyChanged(nameof(BorderColor));
        OnPropertyChanged(nameof(BorderBrush));
        OnPropertyChanged(nameof(AccentColor));
    }

    /// <summary>
    /// Whether the point lies inside <see cref="BodyRect"/> (the zone content area).
    /// </summary>
    /// <param name="x">Canvas X coordinate.</param>
    /// <param name="y">Canvas Y coordinate.</param>
    /// <returns><see langword="true"/> when the point is within the body bounds.</returns>
    public bool ContainsBodyPoint(double x, double y) =>
        x >= BodyRect.X && x <= BodyRect.Right && y >= BodyRect.Y && y <= BodyRect.Bottom;

    /// <summary>
    /// Whether the point lies inside <see cref="InteractionRect"/> (title plus body).
    /// </summary>
    /// <param name="x">Canvas X coordinate.</param>
    /// <param name="y">Canvas Y coordinate.</param>
    /// <returns><see langword="true"/> when the point is within the interaction bounds.</returns>
    public bool ContainsInteractionPoint(double x, double y) =>
        x >= InteractionRect.X && x <= InteractionRect.Right &&
        y >= InteractionRect.Y && y <= InteractionRect.Bottom;

    /// <summary>
    /// All state boxes whose <see cref="StateBoxViewModel.ZoneId"/> matches this zone.
    /// </summary>
    /// <returns>Child boxes in arbitrary order.</returns>
    public IEnumerable<StateBoxViewModel> GetChildBoxes() =>
        Main.Boxes.Where(box => box.ZoneId == Id);

    /// <summary>
    /// Child boxes in visual reading order: top-to-bottom, then left-to-right.
    /// First = zone enter; last = zone exit (pins are direction-agnostic).
    /// </summary>
    /// <returns>Ordered list of child boxes.</returns>
    public IReadOnlyList<StateBoxViewModel> GetOrderedChildBoxes() =>
        GetChildBoxes()
            .OrderBy(box => box.Y)
            .ThenBy(box => box.X)
            .ToList();

    /// <summary>
    /// First child box in visual order, used as the zone entry endpoint.
    /// </summary>
    /// <returns>The enter box, or <see langword="null"/> when the zone has no children.</returns>
    public StateBoxViewModel? GetEnterBox() =>
        GetOrderedChildBoxes().FirstOrDefault();

    /// <summary>
    /// Last child box in visual order, used as the zone exit endpoint.
    /// </summary>
    /// <returns>The exit box, or <see langword="null"/> when the zone has no children.</returns>
    public StateBoxViewModel? GetExitBox()
    {
        var children = GetOrderedChildBoxes();
        return children.Count == 0 ? null : children[^1];
    }

    /// <summary>
    /// Graph-space coordinates of a zone-level connection pin on the body edge.
    /// </summary>
    /// <param name="side">Edge or error pin to locate.</param>
    /// <returns>Pin center in canvas coordinates.</returns>
    public (double X, double Y) GetPinPosition(PinSide side)
    {
        var body = BodyRect;
        return side switch
        {
            PinSide.Left => (body.Left, body.Top + body.Height / 2),
            PinSide.Right => (body.Right, body.Top + body.Height / 2),
            PinSide.Top => (body.Left + body.Width / 2, body.Top),
            PinSide.Bottom => (body.Left + body.Width / 2, body.Bottom),
            PinSide.Error => (body.Right - 4, body.Top + 4),
            _ => (body.Left, body.Top + body.Height / 2)
        };
    }
}

using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using VisualStates.ViewModels;

namespace VisualStates.Controls;

public class CompositionConnectionLayer : Control
{
    public static readonly StyledProperty<MainViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<CompositionConnectionLayer, MainViewModel?>(nameof(ViewModel));

    private readonly DispatcherTimer _animationTimer;
    private double _flowPhase;
    private DateTime _lastAnimationTick = DateTime.UtcNow;
    private MainViewModel? _hookedViewModel;

    private const double FlowSpeed = 0.32;
    private const double DashPeriod = 100.0;

    public CompositionConnectionLayer()
    {
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _animationTimer.Tick += (_, _) =>
        {
            var now = DateTime.UtcNow;
            var deltaSeconds = Math.Min((now - _lastAnimationTick).TotalSeconds, 0.05);
            _lastAnimationTick = now;
            _flowPhase = (_flowPhase + deltaSeconds * FlowSpeed) % 1.0;
            InvalidateVisual();
        };
        _animationTimer.Start();
    }

    public MainViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ViewModelProperty)
            return;

        UnhookViewModel(_hookedViewModel);
        _hookedViewModel = ViewModel;
        if (ViewModel is not null)
            HookViewModel(ViewModel);

        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_hookedViewModel is null && ViewModel is not null)
        {
            _hookedViewModel = ViewModel;
            HookViewModel(ViewModel);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnhookViewModel(_hookedViewModel);
        _hookedViewModel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void HookViewModel(MainViewModel vm)
    {
        vm.Connections.CollectionChanged += OnGraphChanged;
        vm.Boxes.CollectionChanged += OnBoxesCollectionChanged;
        vm.PropertyChanged += OnViewModelPropertyChanged;

        foreach (var connection in vm.Connections)
            connection.PropertyChanged += OnConnectionPropertyChanged;

        foreach (var box in vm.Boxes)
            HookBox(box);
    }

    private void UnhookViewModel(MainViewModel? vm)
    {
        if (vm is null)
            return;

        vm.Connections.CollectionChanged -= OnGraphChanged;
        vm.Boxes.CollectionChanged -= OnBoxesCollectionChanged;
        vm.PropertyChanged -= OnViewModelPropertyChanged;

        foreach (var connection in vm.Connections)
            connection.PropertyChanged -= OnConnectionPropertyChanged;

        foreach (var box in vm.Boxes)
            UnhookBox(box);
    }

    private void HookBox(StateBoxViewModel box)
    {
        box.Steps.CollectionChanged += OnGraphChanged;
        box.PropertyChanged += OnBoxMoved;
    }

    private void UnhookBox(StateBoxViewModel box)
    {
        box.Steps.CollectionChanged -= OnGraphChanged;
        box.PropertyChanged -= OnBoxMoved;
    }

    private void OnBoxesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();

        if (e.NewItems is not null)
        {
            foreach (StateBoxViewModel box in e.NewItems)
                HookBox(box);
        }

        if (e.OldItems is not null)
        {
            foreach (StateBoxViewModel box in e.OldItems)
                UnhookBox(box);
        }
    }

    private void OnBoxMoved(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StateBoxViewModel.X) or nameof(StateBoxViewModel.Y))
            InvalidateVisual();
    }

    private void OnGraphChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();

        if (sender is not System.Collections.ObjectModel.ObservableCollection<ConnectionViewModel>)
            return;

        if (e.NewItems is not null)
        {
            foreach (ConnectionViewModel connection in e.NewItems)
                connection.PropertyChanged += OnConnectionPropertyChanged;
        }

        if (e.OldItems is not null)
        {
            foreach (ConnectionViewModel connection in e.OldItems)
                connection.PropertyChanged -= OnConnectionPropertyChanged;
        }
    }

    private void OnConnectionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConnectionViewModel.IsSelected))
            InvalidateVisual();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MainViewModel.Zoom)
            or nameof(MainViewModel.PanX)
            or nameof(MainViewModel.PanY)
            or nameof(MainViewModel.GraphRevision)
            or nameof(MainViewModel.IsConnecting)
            or nameof(MainViewModel.ConnectionDragEndX)
            or nameof(MainViewModel.ConnectionDragEndY)
            or nameof(MainViewModel.SelectedConnection))
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (ViewModel is null)
            return;

        var zoom = ViewModel.Zoom;
        using (context.PushTransform(new Matrix(zoom, 0, 0, zoom, ViewModel.PanX, ViewModel.PanY)))
        {
            for (var i = 0; i < ViewModel.Connections.Count; i++)
                DrawAnimatedConnection(context, ViewModel.Connections[i], i);

            if (ViewModel.IsConnecting && ViewModel.ConnectionSourceBox is not null)
                DrawDragPreview(context);
        }
    }

    private void DrawDragPreview(DrawingContext context)
    {
        var source = ViewModel!.ConnectionSourceBox!;
        var (x1, y1) = ConnectionRenderHelper.GetPinPosition(source, ViewModel.ConnectionSourceStep, isOutput: true);
        var x2 = ViewModel.ConnectionDragEndX;
        var y2 = ViewModel.ConnectionDragEndY;
        var geometry = ConnectionRenderHelper.CreateRoutedGeometry(x1, y1, x2, y2);

        var wirePen = CreateRoundPen(Color.FromArgb(180, 120, 190, 255), 2.5);
        wirePen.DashStyle = DashStyle.Dash;
        context.DrawGeometry(null, wirePen, geometry);
    }

    private void DrawAnimatedConnection(DrawingContext context, ConnectionViewModel connection, int routeIndex)
    {
        var (x1, y1) = connection.GetSourcePoint();
        var (x2, y2) = connection.GetTargetPoint();
        var geometry = ConnectionRenderHelper.CreateRoutedGeometry(x1, y1, x2, y2, routeIndex);

        var wireColor = connection.IsSelected
            ? Color.FromRgb(255, 140, 0)
            : Color.FromRgb(220, 220, 220);
        var wireWidth = connection.IsSelected ? 3.0 : 2.5;

        context.DrawGeometry(null, CreateRoundPen(wireColor, wireWidth), geometry);

        if (connection.IsSelected)
            DrawInnerGlowFlow(context, geometry, wireWidth, selected: true);
        else
            DrawInnerGlowFlow(context, geometry, wireWidth, selected: false);

        var routePoint = ConnectionRenderHelper.GetRoutePoint(x1, y1, x2, y2, routeIndex);
        var markerColor = connection.IsSelected
            ? Color.FromRgb(255, 140, 0)
            : Color.FromRgb(220, 220, 220);
        DrawDirectionMarker(context, routePoint, markerColor);
    }

    private void DrawInnerGlowFlow(DrawingContext context, StreamGeometry geometry, double wireWidth, bool selected)
    {
        var pulse = 0.9 + 0.1 * Math.Sin(_flowPhase * Math.PI * 2);
        var travelOffset = -_flowPhase * DashPeriod;
        var innerWidth = Math.Max(1.2, wireWidth - 0.8);

        if (selected)
        {
            DrawFlowPulse(context, geometry, Color.FromRgb(180, 40, 0), innerWidth + 1.5, (byte)(100 * pulse), [24.0, 76.0], travelOffset);
            DrawFlowPulse(context, geometry, Color.FromRgb(255, 90, 0), innerWidth + 0.8, (byte)(190 * pulse), [18.0, 82.0], travelOffset);
            DrawFlowPulse(context, geometry, Color.FromRgb(255, 220, 0), innerWidth, (byte)(240 * pulse), [12.0, 88.0], travelOffset);
            DrawFlowPulse(context, geometry, Color.FromRgb(255, 255, 120), innerWidth - 0.2, 255, [6.0, 94.0], travelOffset);
            return;
        }

        DrawFlowPulse(context, geometry, Color.FromRgb(0, 70, 200), innerWidth + 1.5, (byte)(110 * pulse), [24.0, 76.0], travelOffset);
        DrawFlowPulse(context, geometry, Color.FromRgb(0, 120, 255), innerWidth + 0.8, (byte)(200 * pulse), [18.0, 82.0], travelOffset);
        DrawFlowPulse(context, geometry, Color.FromRgb(0, 190, 255), innerWidth, (byte)(245 * pulse), [12.0, 88.0], travelOffset);
        DrawFlowPulse(context, geometry, Color.FromRgb(0, 255, 255), innerWidth - 0.2, 255, [6.0, 94.0], travelOffset);
    }

    private static void DrawFlowPulse(
        DrawingContext context,
        StreamGeometry geometry,
        Color color,
        double width,
        byte alpha,
        IReadOnlyList<double> dash,
        double offset)
    {
        if (alpha <= 0 || width <= 0)
            return;

        var pen = CreateRoundPen(Color.FromArgb(alpha, color.R, color.G, color.B), width);
        pen.DashStyle = new DashStyle(dash, offset);
        context.DrawGeometry(null, pen, geometry);
    }

    private static Pen CreateRoundPen(Color color, double width) =>
        new(new SolidColorBrush(color), width)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

    private static void DrawDirectionMarker(DrawingContext context, RoutePoint routePoint, Color color)
    {
        var size = 7.0;
        var tip = new Point(
            routePoint.X + Math.Cos(routePoint.Angle) * size,
            routePoint.Y + Math.Sin(routePoint.Angle) * size);
        var left = new Point(
            routePoint.X + Math.Cos(routePoint.Angle + 2.5) * size,
            routePoint.Y + Math.Sin(routePoint.Angle + 2.5) * size);
        var right = new Point(
            routePoint.X + Math.Cos(routePoint.Angle - 2.5) * size,
            routePoint.Y + Math.Sin(routePoint.Angle - 2.5) * size);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(tip, true);
            ctx.LineTo(left);
            ctx.LineTo(right);
            ctx.EndFigure(true);
        }

        context.DrawGeometry(new SolidColorBrush(color), null, geo);
    }
}

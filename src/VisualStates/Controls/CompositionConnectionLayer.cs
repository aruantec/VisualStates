using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using VisualStates.Core.Models;
using VisualStates.ViewModels;

namespace VisualStates.Controls;

/// <summary>
/// Avalonia control that draws animated connection wires and the rubber-band drag preview above the graph canvas.
/// </summary>
public class CompositionConnectionLayer : Control
{
    /// <summary>
    /// Identifies the <see cref="ViewModel"/> dependency property.
    /// </summary>
    public static readonly StyledProperty<MainViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<CompositionConnectionLayer, MainViewModel?>(nameof(ViewModel));

    private readonly DispatcherTimer _animationTimer;
    private double _flowPhase;
    private DateTime _lastAnimationTick = DateTime.UtcNow;
    private MainViewModel? _hookedViewModel;

    private const double FlowSpeed = 0.32;
    private const double DashPeriod = 100.0;

    /// <summary>
    /// Creates the connection layer and starts the animation timer used for
    /// the flowing dash effect on wires.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the view model that supplies connection data, pan/zoom state, and drag-preview coordinates.
    /// </summary>
    public MainViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>
    /// Re-hooks the bound <see cref="ViewModel"/> when <see cref="ViewModelProperty"/> changes
    /// and invalidates the visual so wires redraw.
    /// </summary>
    /// <param name="change">Property change details.</param>
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

    /// <summary>
    /// Hooks the current <see cref="ViewModel"/> when the control enters the visual tree
    /// if subscriptions were not already established.
    /// </summary>
    /// <param name="e">Attachment event args.</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_hookedViewModel is null && ViewModel is not null)
        {
            _hookedViewModel = ViewModel;
            HookViewModel(ViewModel);
        }
    }

    /// <summary>
    /// Unhooks the view model and clears the subscription handle when the control
    /// leaves the visual tree.
    /// </summary>
    /// <param name="e">Detachment event args.</param>
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
            or nameof(MainViewModel.ConnectionSourceZone)
            or nameof(MainViewModel.ConnectionHoverZone)
            or nameof(MainViewModel.ConnectionHoverSide)
            or nameof(MainViewModel.SelectedConnection)
            or nameof(MainViewModel.ExecutingConnection))
        {
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Draws all routed connections (with animated flow) and, when a connection
    /// drag is active, the rubber-band preview wire.
    /// </summary>
    /// <param name="context">Drawing context for this frame.</param>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (ViewModel is null)
            return;

        var zoom = ViewModel.Zoom;
        var paths = ConnectionRenderHelper.BuildAllConnectionPaths(ViewModel);
        using (context.PushTransform(new Matrix(zoom, 0, 0, zoom, ViewModel.PanX, ViewModel.PanY)))
        {
            for (var i = 0; i < ViewModel.Connections.Count; i++)
                DrawAnimatedConnection(context, ViewModel.Connections[i], paths[i]);

            if (ViewModel.IsConnecting
                && (ViewModel.ConnectionSourceBox is not null || ViewModel.ConnectionSourceZone is not null))
                DrawDragPreview(context);
        }
    }

    /// <summary>
    /// Draws the dashed rubber-band wire from the connection source pin to the current drag cursor position.
    /// </summary>
    /// <param name="context">The drawing context for the current render pass.</param>
    /// <remarks>
    /// Uses a rubber-band Bezier (not orthogonal stubs) so near-pin drags stay clean.
    /// </remarks>
    private void DrawDragPreview(DrawingContext context)
    {
        var sourceSide = ViewModel!.ConnectionSourceSide;
        double x1, y1;
        if (ViewModel.ConnectionSourceZone is not null)
        {
            (x1, y1) = ViewModel.ConnectionSourceZone.GetPinPosition(sourceSide);
        }
        else
        {
            var source = ViewModel.ConnectionSourceBox!;
            (x1, y1) = ConnectionRenderHelper.GetPinPosition(source, ViewModel.ConnectionSourceStep, sourceSide);
        }

        var x2 = ViewModel.ConnectionDragEndX;
        var y2 = ViewModel.ConnectionDragEndY;

        var wirePen = CreateRoundPen(Color.FromArgb(180, 120, 190, 255), 2.5);
        wirePen.DashStyle = DashStyle.Dash;

        var hoverZone = ViewModel.ConnectionHoverZone;
        var hoverBox = ViewModel.ConnectionHoverBox;
        var hoverSide = ViewModel.ConnectionHoverSide;
        var targetSide = hoverZone is not null || hoverBox is not null
            ? hoverSide
            : InferDragTargetSide(sourceSide, x2, y2, x1, y1);

        var geometry = ConnectionRenderHelper.CreateDragPreviewGeometry(
            x1, y1, sourceSide, x2, y2, targetSide);

        context.DrawGeometry(null, wirePen, geometry);
    }

    /// <summary>
    /// Infers which pin side the drag preview should route toward when the cursor is not over a pin.
    /// </summary>
    /// <param name="sourceSide">The side of the source pin where the drag started.</param>
    /// <param name="x2">The drag cursor X coordinate in graph space.</param>
    /// <param name="y2">The drag cursor Y coordinate in graph space.</param>
    /// <param name="x1">The source pin X coordinate in graph space.</param>
    /// <param name="y1">The source pin Y coordinate in graph space.</param>
    /// <returns>
    /// The inferred target <see cref="PinSide"/> based on the relative position of the cursor to the source pin.
    /// </returns>
    /// <remarks>
    /// Error pins leave toward the right like a normal output.
    /// </remarks>
    private static PinSide InferDragTargetSide(
        PinSide sourceSide, double x2, double y2, double x1, double y1)
    {
        if (sourceSide is PinSide.Top or PinSide.Bottom)
            return y2 < y1 ? PinSide.Bottom : PinSide.Top;

        return x2 < x1 ? PinSide.Right : PinSide.Left;
    }

    /// <summary>
    /// Draws a single routed connection wire with animated inner glow and a direction marker.
    /// </summary>
    /// <param name="context">The drawing context for the current render pass.</param>
    /// <param name="connection">The connection view model whose selection and error state determine wire styling.</param>
    /// <param name="points">The routed polyline points for the connection path.</param>
    private void DrawAnimatedConnection(
        DrawingContext context, ConnectionViewModel connection, IReadOnlyList<Point> points)
    {
        var geometry = ConnectionRenderHelper.CreateRoutedGeometry(points);
        var isExecuting = ViewModel?.ExecutingConnection == connection;

        var wireColor = isExecuting
            ? Color.FromRgb(0, 200, 120)
            : connection.IsSelected
                ? Color.FromRgb(255, 140, 0)
                : connection.IsError
                    ? Color.FromRgb(220, 70, 70)
                    : Color.FromRgb(220, 220, 220);
        var wireWidth = isExecuting || connection.IsSelected ? 3.0 : 2.5;

        context.DrawGeometry(null, CreateRoundPen(wireColor, wireWidth), geometry);

        if (isExecuting || connection.IsSelected)
            DrawInnerGlowFlow(context, geometry, wireWidth, selected: true);
        else
            DrawInnerGlowFlow(context, geometry, wireWidth, selected: false);

        var routePoint = ConnectionRenderHelper.GetRoutePoint(points);
        var markerColor = isExecuting
            ? Color.FromRgb(0, 200, 120)
            : connection.IsSelected
                ? Color.FromRgb(255, 140, 0)
                : Color.FromRgb(220, 220, 220);
        DrawDirectionMarker(context, routePoint, markerColor);
    }

    /// <summary>
    /// Draws layered animated dash pulses along the inner edge of a connection wire to simulate flow.
    /// </summary>
    /// <param name="context">The drawing context for the current render pass.</param>
    /// <param name="geometry">The connection path geometry along which pulses are drawn.</param>
    /// <param name="wireWidth">The outer wire stroke width used to size the inner glow layers.</param>
    /// <param name="selected">
    /// When <see langword="true"/>, uses warm orange/yellow pulse colors; otherwise uses cool blue/cyan tones.
    /// </param>
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

    /// <summary>
    /// Draws a single dashed pulse stroke along a connection path geometry.
    /// </summary>
    /// <param name="context">The drawing context for the current render pass.</param>
    /// <param name="geometry">The path geometry to stroke.</param>
    /// <param name="color">The base RGB color of the pulse; alpha is supplied separately.</param>
    /// <param name="width">The stroke width of the pulse layer.</param>
    /// <param name="alpha">The opacity of the pulse stroke.</param>
    /// <param name="dash">The dash pattern lengths for the animated stroke.</param>
    /// <param name="offset">The dash offset that advances with the animation phase.</param>
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

    /// <summary>
    /// Creates a round-capped, round-joined pen for drawing connection wires and glow layers.
    /// </summary>
    /// <param name="color">The stroke color.</param>
    /// <param name="width">The stroke width.</param>
    /// <returns>A configured <see cref="Pen"/> with round line caps and joins.</returns>
    private static Pen CreateRoundPen(Color color, double width) =>
        new(new SolidColorBrush(color), width)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

    /// <summary>
    /// Draws a filled triangular arrowhead indicating connection flow direction at a route point.
    /// </summary>
    /// <param name="context">The drawing context for the current render pass.</param>
    /// <param name="routePoint">The position and angle along the path where the marker is placed.</param>
    /// <param name="color">The fill color of the direction marker.</param>
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

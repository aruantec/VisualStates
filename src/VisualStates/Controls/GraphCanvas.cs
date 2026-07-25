using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualStates.Core.Models;
using VisualStates.ViewModels;

namespace VisualStates.Controls;

public class GraphCanvas : Control
{
    public const double HeaderHeight = 34;
    public const double StepHeight = 42;
    public const double StepPadding = 12;
    public const double StepInset = 10;

    public static readonly StyledProperty<MainViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<GraphCanvas, MainViewModel?>(nameof(ViewModel));

    private Point _lastPanPoint;
    private bool _isPanning;
    private bool _backgroundViewPan;
    private Point _backgroundViewPanOrigin;
    private ConnectionViewModel? _pendingConnectionSelect;
    private StateBoxViewModel? _dragBox;
    private ZoneViewModel? _dragZone;
    private ZoneViewModel? _resizeZone;
    private ZoneResizeEdge _resizeEdge = ZoneResizeEdge.None;
    private (double X, double Y, double W, double H) _resizeOrigin;
    private Point _resizeStartGraph;
    private Point _dragStart;
    private MainViewModel? _hookedViewModel;
    private bool _isDraggingConnection;

    private const double ViewClickThreshold = 4;

    static GraphCanvas()
    {
        FocusableProperty.OverrideDefaultValue<GraphCanvas>(true);
        ClipToBoundsProperty.OverrideDefaultValue<GraphCanvas>(true);
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
        vm.Boxes.CollectionChanged += OnBoxesCollectionChanged;
        vm.Zones.CollectionChanged += OnZonesCollectionChanged;
        vm.Connections.CollectionChanged += OnGraphCollectionChanged;
        vm.PropertyChanged += OnViewModelPropertyChanged;

        foreach (var box in vm.Boxes)
            HookBox(box);

        foreach (var zone in vm.Zones)
            HookZone(zone);
    }

    private void UnhookViewModel(MainViewModel? vm)
    {
        if (vm is null)
            return;

        vm.Boxes.CollectionChanged -= OnBoxesCollectionChanged;
        vm.Zones.CollectionChanged -= OnZonesCollectionChanged;
        vm.Connections.CollectionChanged -= OnGraphCollectionChanged;
        vm.PropertyChanged -= OnViewModelPropertyChanged;

        foreach (var box in vm.Boxes)
            UnhookBox(box);

        foreach (var zone in vm.Zones)
            UnhookZone(zone);
    }

    private void OnZonesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();

        if (e.NewItems is not null)
        {
            foreach (ZoneViewModel zone in e.NewItems)
                HookZone(zone);
        }

        if (e.OldItems is not null)
        {
            foreach (ZoneViewModel zone in e.OldItems)
                UnhookZone(zone);
        }
    }

    private void HookZone(ZoneViewModel zone) =>
        zone.PropertyChanged += OnZonePropertyChanged;

    private void UnhookZone(ZoneViewModel zone) =>
        zone.PropertyChanged -= OnZonePropertyChanged;

    private void OnZonePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ZoneViewModel.X)
            or nameof(ZoneViewModel.Y)
            or nameof(ZoneViewModel.Width)
            or nameof(ZoneViewModel.Height)
            or nameof(ZoneViewModel.IsSelected)
            or nameof(ZoneViewModel.Name)
            or nameof(ZoneViewModel.BorderColor)
            or nameof(ZoneViewModel.BorderBrush))
        {
            InvalidateVisual();
        }
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

    private void OnGraphCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        InvalidateVisual();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MainViewModel.Zoom)
            or nameof(MainViewModel.PanX)
            or nameof(MainViewModel.PanY)
            or nameof(MainViewModel.IsConnecting)
            or nameof(MainViewModel.ConnectionHoverBox)
            or nameof(MainViewModel.ConnectionHoverStep)
            or nameof(MainViewModel.ConnectionHoverZone)
            or nameof(MainViewModel.ConnectionSourceZone)
            or nameof(MainViewModel.GraphRevision)
            or nameof(MainViewModel.SelectedBox)
            or nameof(MainViewModel.SelectedStep)
            or nameof(MainViewModel.SelectedZone)
            or nameof(MainViewModel.ZoneDropTarget)
            or nameof(MainViewModel.SelectedConnection))
        {
            InvalidateVisual();
        }
    }

    private void HookBox(StateBoxViewModel box)
    {
        box.PropertyChanged += OnBoxPropertyChanged;
        box.Steps.CollectionChanged += OnBoxStepsChanged;

        foreach (var step in box.Steps)
            step.PropertyChanged += OnStepPropertyChanged;
    }

    private void UnhookBox(StateBoxViewModel box)
    {
        box.PropertyChanged -= OnBoxPropertyChanged;
        box.Steps.CollectionChanged -= OnBoxStepsChanged;

        foreach (var step in box.Steps)
            step.PropertyChanged -= OnStepPropertyChanged;
    }

    private void OnBoxStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();

        if (e.NewItems is not null)
        {
            foreach (StateStepViewModel step in e.NewItems)
                step.PropertyChanged += OnStepPropertyChanged;
        }

        if (e.OldItems is not null)
        {
            foreach (StateStepViewModel step in e.OldItems)
                step.PropertyChanged -= OnStepPropertyChanged;
        }
    }

    private void OnBoxPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(StateBoxViewModel.X)
            or nameof(StateBoxViewModel.Y)
            or nameof(StateBoxViewModel.Width)
            or nameof(StateBoxViewModel.IsSelected)
            or nameof(StateBoxViewModel.Name)
            or nameof(StateBoxViewModel.IsEntry)
            or nameof(StateBoxViewModel.HeaderColor)
            or nameof(StateBoxViewModel.HeaderBrush))
        {
            InvalidateVisual();
        }
    }

    private void OnStepPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) =>
        InvalidateVisual();

    public MainViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (ViewModel is null)
            return;

        ApplyZoomAt(e.GetPosition(this), GraphViewport.GetWheelZoomFactor(e.Delta));
        e.Handled = true;
    }

    internal void ApplyZoomAt(Point screenPoint, double factor)
    {
        if (ViewModel is null)
            return;

        var (panX, panY, zoom) = GraphViewport.ZoomAt(
            screenPoint,
            ViewModel.PanX,
            ViewModel.PanY,
            ViewModel.Zoom,
            factor);

        ViewModel.PanX = panX;
        ViewModel.PanY = panY;
        ViewModel.Zoom = zoom;
        InvalidateVisual();
    }

    private void StartViewPan(IPointer pointer, Point screenPoint)
    {
        _isPanning = true;
        _lastPanPoint = screenPoint;
        pointer.Capture(this);
    }

    private void EndViewPan(IPointer? pointer)
    {
        if (!_isPanning)
            return;

        _isPanning = false;
        pointer?.Capture(null);
    }

    internal bool IsViewPanning => _isPanning;

    internal void HandlePointerPressed(PointerPressedEventArgs e) => OnPointerPressed(e);

    internal void HandlePointerMoved(PointerEventArgs e) => OnPointerMoved(e);

    internal void HandlePointerReleased(PointerReleasedEventArgs e) => OnPointerReleased(e);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsMiddleButtonPressed ||
            (point.Properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
        {
            _backgroundViewPan = false;
            StartViewPan(e.Pointer, point.Position);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed || ViewModel is null)
            return;

        var screenPoint = point.Position;
        var graphPoint = ScreenToGraph(screenPoint);

        var pin = HitTestPin(graphPoint, inputOnly: false, outputOnly: false);
        if (pin is not null)
        {
            if (pin.Value.IsOutput)
            {
                if (pin.Value.Zone is not null)
                    ViewModel.StartConnectionDragFromZone(pin.Value.Zone, pin.Value.Side, graphPoint.X, graphPoint.Y);
                else
                    ViewModel.StartConnectionDrag(pin.Value.Box!, pin.Value.Step, pin.Value.Side, graphPoint.X, graphPoint.Y);

                if (ViewModel.IsConnecting)
                {
                    _isDraggingConnection = true;
                    Focus();
                    e.Pointer.Capture(this);
                    e.Handled = true;
                    InvalidateVisual();
                }

                return;
            }

            if (ViewModel.IsConnecting)
            {
                if (pin.Value.Zone is not null)
                    ViewModel.TryCompleteConnectionDragToZone(pin.Value.Zone, pin.Value.Side);
                else
                    ViewModel.TryCompleteConnectionDrag(pin.Value.Box!, pin.Value.Step, pin.Value.Side);

                _isDraggingConnection = false;
                e.Pointer.Capture(null);
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (pin.Value.Box is not null)
            {
                ViewModel.SelectBoxCommand.Execute(pin.Value.Box);
                _dragBox = pin.Value.Box;
                _dragStart = graphPoint;
                e.Handled = true;
            }

            return;
        }

        var (hitBox, hitStep) = HitTest(graphPoint);
        if (hitBox is not null)
        {
            if (hitStep is not null)
                ViewModel.SelectStepCommand.Execute(hitStep);
            else
                ViewModel.SelectBoxCommand.Execute(hitBox);

            _dragBox = hitBox;
            _dragStart = graphPoint;
            e.Handled = true;
            return;
        }

        var (hitZone, zoneEdge) = HitTestZoneInteraction(graphPoint);
        if (hitZone is not null)
        {
            ViewModel.SelectZoneCommand.Execute(hitZone);
            if (zoneEdge is not (ZoneResizeEdge.None or ZoneResizeEdge.Move))
            {
                _resizeZone = hitZone;
                _resizeEdge = zoneEdge;
                _resizeOrigin = (hitZone.X, hitZone.Y, hitZone.Width, hitZone.Height);
                _resizeStartGraph = graphPoint;
                UpdateZoneCursor(zoneEdge);
            }
            else
            {
                _dragZone = hitZone;
                _dragStart = graphPoint;
                UpdateZoneCursor(ZoneResizeEdge.Move);
            }

            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        _backgroundViewPan = true;
        _backgroundViewPanOrigin = screenPoint;
        _pendingConnectionSelect = ConnectionRenderHelper.FindConnectionAtScreen(ViewModel, screenPoint);
        StartViewPan(e.Pointer, screenPoint);
        Focus();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (ViewModel is null)
            return;

        if (_isPanning)
        {
            var point = e.GetCurrentPoint(this).Position;
            ViewModel.PanX += point.X - _lastPanPoint.X;
            ViewModel.PanY += point.Y - _lastPanPoint.Y;
            _lastPanPoint = point;
            InvalidateVisual();
            return;
        }

        if (_isDraggingConnection)
        {
            var graphPoint = ScreenToGraph(e.GetCurrentPoint(this).Position);
            var inputPin = HitTestPin(graphPoint, inputOnly: true, outputOnly: false);
            ViewModel.UpdateConnectionDrag(
                graphPoint.X,
                graphPoint.Y,
                inputPin?.Box,
                inputPin?.Step,
                inputPin?.Side ?? PinSide.Left,
                inputPin?.Zone);
            InvalidateVisual();
            return;
        }

        if (_dragBox is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var graphPoint = ScreenToGraph(e.GetCurrentPoint(this).Position);
            var deltaX = graphPoint.X - _dragStart.X;
            var deltaY = graphPoint.Y - _dragStart.Y;
            if (Math.Abs(deltaX) > 0.5 || Math.Abs(deltaY) > 0.5)
            {
                _dragBox.X += deltaX;
                _dragBox.Y += deltaY;
                ClampBoxToCanvas(_dragBox);
                _dragStart = graphPoint;
                ViewModel.UpdateZoneDropTarget(_dragBox);
                InvalidateVisual();
            }

            return;
        }

        if (_resizeZone is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var graphPoint = ScreenToGraph(e.GetCurrentPoint(this).Position);
            var deltaX = graphPoint.X - _resizeStartGraph.X;
            var deltaY = graphPoint.Y - _resizeStartGraph.Y;
            ApplyZoneResize(_resizeZone, _resizeEdge, deltaX, deltaY);
            UpdateZoneCursor(_resizeEdge);
            InvalidateVisual();
            return;
        }

        if (_dragZone is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var graphPoint = ScreenToGraph(e.GetCurrentPoint(this).Position);
            var deltaX = graphPoint.X - _dragStart.X;
            var deltaY = graphPoint.Y - _dragStart.Y;
            if (Math.Abs(deltaX) > 0.5 || Math.Abs(deltaY) > 0.5)
            {
                _dragZone.X += deltaX;
                _dragZone.Y += deltaY;
                foreach (var child in _dragZone.GetChildBoxes())
                {
                    child.X += deltaX;
                    child.Y += deltaY;
                    ClampBoxToCanvas(child);
                }

                _dragStart = graphPoint;
                InvalidateVisual();
            }

            return;
        }

        UpdateZoneHoverCursor(e.GetCurrentPoint(this).Position);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ResetCursor();
    }

    private void UpdateZoneHoverCursor(Point screenPoint)
    {
        if (ViewModel is null || _resizeZone is not null || _dragBox is not null || _dragZone is not null)
            return;

        var graphPoint = ScreenToGraph(screenPoint);
        var (hitBox, _) = HitTest(graphPoint);
        if (hitBox is not null)
        {
            ResetCursor();
            return;
        }

        var (_, edge) = HitTestZoneInteraction(graphPoint);
        if (edge == ZoneResizeEdge.None)
            ResetCursor();
        else
            UpdateZoneCursor(edge);
    }

    private void UpdateZoneCursor(ZoneResizeEdge edge)
    {
        Cursor = new Cursor(ZoneResizeHelper.GetCursor(edge));
    }

    private void ResetCursor()
    {
        Cursor = Cursor.Default;
    }

    private void ClampBoxToCanvas(StateBoxViewModel box)
    {
        if (ViewModel is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        const double margin = 16;
        var zoom = ViewModel.Zoom;
        var minX = (-ViewModel.PanX / zoom) + margin;
        var minY = (-ViewModel.PanY / zoom) + margin;
        var maxX = ((Bounds.Width - ViewModel.PanX) / zoom) - box.Width - margin;
        var maxY = ((Bounds.Height - ViewModel.PanY) / zoom) - box.GetTotalHeight() - margin;

        box.X = Math.Clamp(box.X, minX, Math.Max(minX, maxX));
        box.Y = Math.Clamp(box.Y, minY, Math.Max(minY, maxY));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isDraggingConnection && ViewModel is not null)
        {
            var graphPoint = ScreenToGraph(e.GetCurrentPoint(this).Position);
            var inputPin = HitTestPin(graphPoint, inputOnly: true, outputOnly: false);
            if (inputPin is not null)
            {
                if (inputPin.Value.Zone is not null)
                    ViewModel.TryCompleteConnectionDragToZone(inputPin.Value.Zone, inputPin.Value.Side);
                else
                    ViewModel.TryCompleteConnectionDrag(inputPin.Value.Box!, inputPin.Value.Step, inputPin.Value.Side);
            }
            else
                ViewModel.CancelConnectionCommand.Execute(null);

            _isDraggingConnection = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }

        if (_backgroundViewPan && ViewModel is not null)
        {
            var releasePoint = e.GetCurrentPoint(this).Position;
            var deltaX = releasePoint.X - _backgroundViewPanOrigin.X;
            var deltaY = releasePoint.Y - _backgroundViewPanOrigin.Y;
            if (deltaX * deltaX + deltaY * deltaY < ViewClickThreshold * ViewClickThreshold)
            {
                if (_pendingConnectionSelect is not null)
                    ViewModel.SelectConnectionCommand.Execute(_pendingConnectionSelect);
                else
                    ViewModel.ClearSelectionCommand.Execute(null);
            }
        }

        _backgroundViewPan = false;
        _pendingConnectionSelect = null;
        EndViewPan(e.Pointer);
        if (_dragBox is not null && ViewModel is not null)
        {
            ClampBoxToCanvas(_dragBox);
            ViewModel.ApplyBoxZoneDrop(_dragBox);
            ViewModel.MarkLayoutChanged();
        }

        _dragBox = null;
        ViewModel?.UpdateZoneDropTarget(null);

        if (_resizeZone is not null && ViewModel is not null)
            ViewModel.MarkLayoutChanged();

        _resizeZone = null;
        _resizeEdge = ZoneResizeEdge.None;

        if (_dragZone is not null && ViewModel is not null)
            ViewModel.MarkLayoutChanged();

        _dragZone = null;
        ResetCursor();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (ViewModel is null)
            return;

        if (e.Key == Key.Delete)
        {
            ViewModel.DeleteSelectionCommand.Execute(null);
            e.Handled = true;
            InvalidateVisual();
            return;
        }

        if (e.Key == Key.Escape && _isDraggingConnection)
        {
            ViewModel.CancelConnectionCommand.Execute(null);
            _isDraggingConnection = false;
            e.Handled = true;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (ViewModel is null)
            return;

        var zoom = ViewModel.Zoom;
        var panX = ViewModel.PanX;
        var panY = ViewModel.PanY;

        using (context.PushTransform(new Matrix(zoom, 0, 0, zoom, panX, panY)))
        {
            DrawGrid(context);
            DrawZones(context);
            DrawBoxes(context);
        }
    }

    private void DrawZones(DrawingContext context)
    {
        foreach (var zone in ViewModel!.Zones)
            DrawZone(context, zone);
    }

    private void DrawZone(DrawingContext context, ZoneViewModel zone)
    {
        var accent = zone.AccentColor;
        var isDropTarget = ViewModel!.ZoneDropTarget == zone;
        var isSelected = zone.IsSelected;
        var bodyRect = zone.BodyRect;

        var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
        var titleText = new FormattedText(
            zone.Name,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            12,
            Brushes.White);

        var titleWidth = Math.Clamp(titleText.Width + 28, 72, zone.Width);
        var titleRect = new Rect(zone.X, zone.Y, titleWidth, ZoneLayout.TitleHeight);

        var titleGradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(255, accent.R, accent.G, accent.B), 0),
                new GradientStop(Color.FromArgb(255,
                    (byte)(accent.R * 0.55),
                    (byte)(accent.G * 0.55),
                    (byte)(accent.B * 0.55)), 1)
            }
        };

        var titleBorder = new Pen(new SolidColorBrush(Color.FromArgb(220, accent.R, accent.G, accent.B)), 1.2);
        context.DrawRectangle(titleGradient, titleBorder, titleRect, 6, 6);
        context.DrawText(titleText, new Point(zone.X + 12, zone.Y + 5));

        context.DrawLine(
            new Pen(new SolidColorBrush(Color.FromArgb(160, accent.R, accent.G, accent.B)), 1.5),
            new Point(zone.X + 10, titleRect.Bottom),
            new Point(zone.X + 10, bodyRect.Top - 2));

        var fillAlpha = (byte)(isDropTarget ? 48 : 22);
        var bodyFill = new SolidColorBrush(Color.FromArgb(fillAlpha, accent.R, accent.G, accent.B));
        IBrush borderBrush = isSelected
            ? new SolidColorBrush(Color.FromRgb(255, 140, 0))
            : new SolidColorBrush(Color.FromArgb((byte)(isDropTarget ? 230 : 170), accent.R, accent.G, accent.B));

        var borderWidth = isSelected ? 2.5 : isDropTarget ? 2.2 : 1.6;
        var borderPen = new Pen(borderBrush, borderWidth)
        {
            DashStyle = DashStyle.Dash
        };

        if (isDropTarget)
        {
            var glowRect = bodyRect.Inflate(5);
            context.DrawRectangle(
                null,
                new Pen(new SolidColorBrush(Color.FromArgb(90, accent.R, accent.G, accent.B)), 4),
                glowRect,
                ZoneLayout.BodyCornerRadius + 2,
                ZoneLayout.BodyCornerRadius + 2);
        }

        context.DrawRectangle(bodyFill, borderPen, bodyRect, ZoneLayout.BodyCornerRadius, ZoneLayout.BodyCornerRadius);

        DrawPin(context, zone.GetPinPosition(PinSide.Left), PinSide.Left, IsZonePinHighlighted(zone, PinSide.Left));
        DrawPin(context, zone.GetPinPosition(PinSide.Right), PinSide.Right, IsZonePinHighlighted(zone, PinSide.Right));
        DrawPin(context, zone.GetPinPosition(PinSide.Top), PinSide.Top, IsZonePinHighlighted(zone, PinSide.Top));
        DrawPin(context, zone.GetPinPosition(PinSide.Bottom), PinSide.Bottom, IsZonePinHighlighted(zone, PinSide.Bottom));

        if (isSelected)
            DrawZoneResizeHandles(context, bodyRect, ViewModel!.Zoom);
    }

    private static void DrawZoneResizeHandles(DrawingContext context, Rect bodyRect, double zoom)
    {
        var size = Math.Max(ZoneLayout.HandleSize, ZoneLayout.CornerHandleScreen / Math.Max(zoom, 0.1));
        var handleBrush = new SolidColorBrush(Color.FromRgb(255, 140, 0));
        var handlePen = new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 42)), 1);

        foreach (var center in GetCornerHandleCenters(bodyRect))
        {
            var handleRect = new Rect(center.X - size / 2, center.Y - size / 2, size, size);
            context.DrawRectangle(handleBrush, handlePen, handleRect, 2, 2);
        }
    }

    private static IEnumerable<Point> GetCornerHandleCenters(Rect bodyRect)
    {
        yield return new Point(bodyRect.Left, bodyRect.Top);
        yield return new Point(bodyRect.Right, bodyRect.Top);
        yield return new Point(bodyRect.Right, bodyRect.Bottom);
        yield return new Point(bodyRect.Left, bodyRect.Bottom);
    }

    private (ZoneViewModel? Zone, ZoneResizeEdge Edge) HitTestZoneInteraction(Point graphPoint)
    {
        if (ViewModel is null)
            return (null, ZoneResizeEdge.None);

        var zoom = ViewModel.Zoom;
        var slop = ZoneResizeHelper.GraphSlop(zoom);

        for (var i = ViewModel.Zones.Count - 1; i >= 0; i--)
        {
            var zone = ViewModel.Zones[i];
            if (!zone.ContainsInteractionPoint(graphPoint.X, graphPoint.Y))
                continue;

            var bodyRect = zone.BodyRect;
            var edge = ZoneResizeHelper.HitTestBody(bodyRect, graphPoint, zoom);
            if (edge != ZoneResizeEdge.Move)
                return (zone, edge);

            if (graphPoint.Y < bodyRect.Top - slop)
                return (zone, ZoneResizeEdge.Move);

            if (bodyRect.Contains(graphPoint) || bodyRect.Inflate(slop).Contains(graphPoint))
                return (zone, ZoneResizeEdge.Move);

            return (zone, ZoneResizeEdge.Move);
        }

        return (null, ZoneResizeEdge.None);
    }

    private void ApplyZoneResize(ZoneViewModel zone, ZoneResizeEdge edge, double deltaX, double deltaY)
    {
        var (x, y, width, height) = ZoneResizeHelper.Apply(
            _resizeOrigin.X,
            _resizeOrigin.Y,
            _resizeOrigin.W,
            _resizeOrigin.H,
            edge,
            deltaX,
            deltaY);

        zone.X = x;
        zone.Y = y;
        zone.Width = width;
        zone.Height = height;
    }

    private void DrawGrid(DrawingContext context)
    {
        if (ViewModel is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        const double gridSize = 32;
        var zoom = ViewModel.Zoom;
        var left = -ViewModel.PanX / zoom;
        var top = -ViewModel.PanY / zoom;
        var right = left + Bounds.Width / zoom;
        var bottom = top + Bounds.Height / zoom;

        var startX = Math.Floor(left / gridSize) * gridSize;
        var startY = Math.Floor(top / gridSize) * gridSize;

        var gridBrush = new SolidColorBrush(Color.FromArgb(30, 180, 180, 180));
        var pen = new Pen(gridBrush, 1.0 / zoom);

        for (var x = startX; x <= right; x += gridSize)
            context.DrawLine(pen, new Point(x, top), new Point(x, bottom));

        for (var y = startY; y <= bottom; y += gridSize)
            context.DrawLine(pen, new Point(left, y), new Point(right, y));
    }

    private void DrawBoxes(DrawingContext context)
    {
        foreach (var box in ViewModel!.Boxes)
            DrawBox(context, box);
    }

    private void DrawBox(DrawingContext context, StateBoxViewModel box)
    {
        var bodyHeight = Math.Max(1, box.Steps.Count) * StepHeight + StepPadding * 2;
        var totalHeight = HeaderHeight + bodyHeight;
        var rect = new Rect(box.X, box.Y, box.Width, totalHeight);
        var isDragging = _dragBox == box;
        var dropTarget = ViewModel!.ZoneDropTarget;

        if (isDragging && dropTarget is not null)
        {
            var accent = dropTarget.AccentColor;
            var glowRect = rect.Inflate(4);
            context.DrawRectangle(
                null,
                new Pen(new SolidColorBrush(Color.FromArgb(140, accent.R, accent.G, accent.B)), 3),
                glowRect,
                8,
                8);
        }
        else if (isDragging)
        {
            var glowRect = rect.Inflate(3);
            context.DrawRectangle(
                null,
                new Pen(new SolidColorBrush(Color.FromArgb(80, 180, 180, 190)), 2),
                glowRect,
                8,
                8);
        }

        var headerBrush = box.HeaderBrush;
        var bodyBrush = new SolidColorBrush(Color.FromRgb(35, 35, 38));
        var borderBrush = box.IsSelected && ViewModel!.SelectedStep is null
            ? new SolidColorBrush(Color.FromRgb(255, 140, 0))
            : new SolidColorBrush(Color.FromRgb(70, 70, 75));

        context.DrawRectangle(bodyBrush, new Pen(borderBrush, box.IsSelected ? 2.5 : 1.5), rect, 6, 6);
        context.DrawRectangle(headerBrush, null, new Rect(box.X, box.Y, box.Width, HeaderHeight), 6, 6);

        var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
        var text = new FormattedText(
            box.Name,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            13,
            Brushes.White);

        context.DrawText(text, new Point(box.X + 12, box.Y + 8));

        var y = box.Y + HeaderHeight + StepPadding;
        foreach (var step in box.Steps)
        {
            DrawStep(context, box, step, y);
            y += StepHeight;
        }

        DrawPin(context, box.GetBoxPinPosition(PinSide.Left), PinSide.Left, IsPinHighlighted(box, null, PinSide.Left));
        DrawPin(context, box.GetBoxPinPosition(PinSide.Right), PinSide.Right, IsPinHighlighted(box, null, PinSide.Right));
        DrawPin(context, box.GetBoxPinPosition(PinSide.Top), PinSide.Top, IsPinHighlighted(box, null, PinSide.Top));
        DrawPin(context, box.GetBoxPinPosition(PinSide.Bottom), PinSide.Bottom, IsPinHighlighted(box, null, PinSide.Bottom));
    }

    private void DrawStep(DrawingContext context, StateBoxViewModel box, StateStepViewModel step, double y)
    {
        var rect = GetStepRect(box, y);
        var isSelected = ViewModel?.SelectedStep == step;
        var accent = step.Kind switch
        {
            StepKind.SetVariable => Color.FromRgb(70, 150, 90),
            StepKind.CallEvent => Color.FromRgb(180, 60, 60),
            StepKind.CallMethod => Color.FromRgb(45, 110, 180),
            _ => Color.FromRgb(90, 90, 95)
        };

        var fill = isSelected
            ? Color.FromRgb(62, 62, 70)
            : Color.FromRgb(48, 48, 52);

        context.DrawRectangle(
            new SolidColorBrush(fill),
            new Pen(new SolidColorBrush(isSelected ? Color.FromRgb(255, 140, 0) : accent), isSelected ? 2 : 1.5),
            rect,
            4,
            4);

        var typeface = new Typeface(FontFamily.Default);
        var text = new FormattedText(
            step.KindLabel,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            11,
            new SolidColorBrush(Color.FromRgb(210, 210, 210)));

        context.DrawText(text, new Point(rect.X + 8, rect.Y + 4));

        var detail = step.Kind switch
        {
            StepKind.SetVariable => $"{step.TargetName} = {step.Expression}",
            StepKind.CallEvent => step.EventName ?? step.TargetName,
            StepKind.CallMethod => step.MethodName ?? step.TargetName,
            _ => step.Name
        } ?? step.Name;

        var detailText = new FormattedText(
            detail ?? string.Empty,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            10,
            new SolidColorBrush(Color.FromRgb(150, 150, 155)));

        context.DrawText(detailText, new Point(rect.X + 8, rect.Y + 18));

        DrawPin(context, box.GetStepPinPosition(step, PinSide.Left), PinSide.Left, IsPinHighlighted(box, step, PinSide.Left));
        DrawPin(context, box.GetStepPinPosition(step, PinSide.Right), PinSide.Right, IsPinHighlighted(box, step, PinSide.Right));
        DrawPin(context, box.GetStepPinPosition(step, PinSide.Top), PinSide.Top, IsPinHighlighted(box, step, PinSide.Top));
        DrawPin(context, box.GetStepPinPosition(step, PinSide.Bottom), PinSide.Bottom, IsPinHighlighted(box, step, PinSide.Bottom));
    }

    private static Rect GetStepRect(StateBoxViewModel box, double y) =>
        new(box.X + StepInset, y, box.Width - StepInset * 2, 34);

    private bool IsPinHighlighted(StateBoxViewModel box, StateStepViewModel? step, PinSide side)
    {
        if (ViewModel is null || !ViewModel.IsConnecting)
            return false;

        var isOutput = side is PinSide.Right or PinSide.Bottom;
        if (isOutput)
        {
            return ViewModel.ConnectionSourceZone is null
                && ViewModel.ConnectionSourceBox == box
                && ViewModel.ConnectionSourceStep == step
                && ViewModel.ConnectionSourceSide == side;
        }

        return ViewModel.ConnectionHoverZone is null
            && ViewModel.ConnectionHoverBox == box
            && ViewModel.ConnectionHoverStep == step
            && ViewModel.ConnectionHoverSide == side;
    }

    private bool IsZonePinHighlighted(ZoneViewModel zone, PinSide side)
    {
        if (ViewModel is null || !ViewModel.IsConnecting)
            return false;

        var isOutput = side is PinSide.Right or PinSide.Bottom;
        if (isOutput)
            return ViewModel.ConnectionSourceZone == zone && ViewModel.ConnectionSourceSide == side;

        return ViewModel.ConnectionHoverZone == zone && ViewModel.ConnectionHoverSide == side;
    }

    private static void DrawPin(DrawingContext context, (double X, double Y) point, PinSide side, bool isHighlighted)
    {
        var isOutput = side is PinSide.Right or PinSide.Bottom;
        var fill = isHighlighted
            ? Color.FromRgb(255, 140, 0)
            : isOutput ? Color.FromRgb(240, 240, 240) : Color.FromRgb(200, 200, 200);
        var radius = isHighlighted ? 7.5 : 6;
        context.DrawEllipse(
            new SolidColorBrush(fill),
            new Pen(new SolidColorBrush(isHighlighted ? Color.FromRgb(255, 200, 120) : Color.FromRgb(30, 30, 30)), isHighlighted ? 2 : 1),
            new Point(point.X, point.Y),
            radius,
            radius);
    }

    private Point ScreenToGraph(Point screenPoint) =>
        GraphViewport.ScreenToGraph(screenPoint, ViewModel!.PanX, ViewModel.PanY, ViewModel.Zoom);

    private (StateBoxViewModel? Box, StateStepViewModel? Step) HitTest(Point graphPoint)
    {
        for (var i = ViewModel!.Boxes.Count - 1; i >= 0; i--)
        {
            var box = ViewModel.Boxes[i];
            var y = box.Y + HeaderHeight + StepPadding;
            foreach (var step in box.Steps)
            {
                if (GetStepRect(box, y).Contains(graphPoint))
                    return (box, step);

                y += StepHeight;
            }

            var height = HeaderHeight + Math.Max(1, box.Steps.Count) * StepHeight + StepPadding * 2;
            var rect = new Rect(box.X, box.Y, box.Width, height);
            if (rect.Contains(graphPoint))
                return (box, null);
        }

        return (null, null);
    }

    private GraphPin? HitTestPin(Point graphPoint, bool inputOnly, bool outputOnly)
    {
        for (var i = ViewModel!.Boxes.Count - 1; i >= 0; i--)
        {
            var box = ViewModel.Boxes[i];
            foreach (var step in box.Steps)
            {
                if (!inputOnly)
                {
                    var right = box.GetStepPinPosition(step, PinSide.Right);
                    if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, right.X, right.Y))
                        return new GraphPin(box, Zone: null, step, IsOutput: true, Side: PinSide.Right);

                    var bottom = box.GetStepPinPosition(step, PinSide.Bottom);
                    if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, bottom.X, bottom.Y))
                        return new GraphPin(box, Zone: null, step, IsOutput: true, Side: PinSide.Bottom);
                }

                if (!outputOnly)
                {
                    var left = box.GetStepPinPosition(step, PinSide.Left);
                    if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, left.X, left.Y))
                        return new GraphPin(box, Zone: null, step, IsOutput: false, Side: PinSide.Left);

                    var top = box.GetStepPinPosition(step, PinSide.Top);
                    if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, top.X, top.Y))
                        return new GraphPin(box, Zone: null, step, IsOutput: false, Side: PinSide.Top);
                }
            }

            if (!inputOnly)
            {
                var boxRight = box.GetBoxPinPosition(PinSide.Right);
                if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, boxRight.X, boxRight.Y))
                    return new GraphPin(box, Zone: null, null, IsOutput: true, Side: PinSide.Right);

                var boxBottom = box.GetBoxPinPosition(PinSide.Bottom);
                if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, boxBottom.X, boxBottom.Y))
                    return new GraphPin(box, Zone: null, null, IsOutput: true, Side: PinSide.Bottom);
            }

            if (!outputOnly)
            {
                var boxLeft = box.GetBoxPinPosition(PinSide.Left);
                if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, boxLeft.X, boxLeft.Y))
                    return new GraphPin(box, Zone: null, null, IsOutput: false, Side: PinSide.Left);

                var boxTop = box.GetBoxPinPosition(PinSide.Top);
                if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, boxTop.X, boxTop.Y))
                    return new GraphPin(box, Zone: null, null, IsOutput: false, Side: PinSide.Top);
            }
        }

        for (var i = ViewModel.Zones.Count - 1; i >= 0; i--)
        {
            var zone = ViewModel.Zones[i];
            if (!inputOnly)
            {
                var right = zone.GetPinPosition(PinSide.Right);
                if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, right.X, right.Y))
                    return new GraphPin(Box: null, zone, Step: null, IsOutput: true, Side: PinSide.Right);

                var bottom = zone.GetPinPosition(PinSide.Bottom);
                if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, bottom.X, bottom.Y))
                    return new GraphPin(Box: null, zone, Step: null, IsOutput: true, Side: PinSide.Bottom);
            }

            if (!outputOnly)
            {
                var left = zone.GetPinPosition(PinSide.Left);
                if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, left.X, left.Y))
                    return new GraphPin(Box: null, zone, Step: null, IsOutput: false, Side: PinSide.Left);

                var top = zone.GetPinPosition(PinSide.Top);
                if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, top.X, top.Y))
                    return new GraphPin(Box: null, zone, Step: null, IsOutput: false, Side: PinSide.Top);
            }
        }

        return null;
    }
}

using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualStates.Core.Models;
using VisualStates.ViewModels;

namespace VisualStates.Controls;

/// <summary>
/// Custom Avalonia control that renders the state graph (grid, zones, boxes, pins)
/// and handles pointer interaction including pan, zoom, box/zone drag, and connection drag.
/// </summary>
public class GraphCanvas : Control
{
    /// <summary>Height of a state box header, in graph coordinates.</summary>
    public const double HeaderHeight = 34;

    /// <summary>Height of each step row inside a state box, in graph coordinates.</summary>
    public const double StepHeight = 42;

    /// <summary>Vertical padding around the step list within a box body.</summary>
    public const double StepPadding = 12;

    /// <summary>Horizontal inset applied to step rectangles from the box edges.</summary>
    public const double StepInset = 10;

    /// <summary>
    /// Identifies the <see cref="ViewModel"/> dependency property.
    /// </summary>
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

    private readonly DispatcherTimer _zoomTimer;
    private bool _zoomAnimating;
    private double _zoomTarget = 1.0;
    private Point _zoomAnchorScreen;
    private Point _zoomAnchorGraph;
    private DateTime _lastZoomTick = DateTime.UtcNow;

    private const double ViewClickThreshold = 4;

    /// <summary>Initializes the control and the smooth zoom animation timer.</summary>
    public GraphCanvas()
    {
        _zoomTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _zoomTimer.Tick += OnZoomAnimationTick;
    }

    static GraphCanvas()
    {
        FocusableProperty.OverrideDefaultValue<GraphCanvas>(true);
        ClipToBoundsProperty.OverrideDefaultValue<GraphCanvas>(true);
    }

    /// <inheritdoc />
    /// <remarks>Subscribes to or unsubscribes from the bound <see cref="ViewModel"/> when it changes.</remarks>
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

    /// <inheritdoc />
    /// <remarks>Hooks the <see cref="ViewModel"/> when the control is attached if not already hooked.</remarks>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_hookedViewModel is null && ViewModel is not null)
        {
            _hookedViewModel = ViewModel;
            HookViewModel(ViewModel);
        }
    }

    /// <inheritdoc />
    /// <remarks>Stops zoom animation and unhooks the view model when detached.</remarks>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopZoomAnimation();
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

    /// <summary>
    /// Gets or sets the view model that supplies graph data and receives interaction commands.
    /// </summary>
    public MainViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc />
    /// <remarks>Applies smooth zoom centered on the wheel position via <see cref="ApplyZoomAt"/>.</remarks>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (ViewModel is null)
            return;

        ApplyZoomAt(e.GetPosition(this), GraphViewport.GetWheelZoomFactor(e.Delta));
        e.Handled = true;
    }

    /// <summary>
    /// Zooms the graph by <paramref name="factor"/> while keeping the graph point under
    /// <paramref name="screenPoint"/> fixed on screen.
    /// </summary>
    /// <param name="screenPoint">Cursor position in control coordinates.</param>
    /// <param name="factor">Multiplicative zoom factor (values above 1 zoom in).</param>
    internal void ApplyZoomAt(Point screenPoint, double factor)
    {
        if (ViewModel is null || Math.Abs(factor - 1.0) < 1e-6)
            return;

        BeginOrRetargetSmoothZoom(screenPoint, factor);
    }

    /// <summary>
    /// Starts or retargets smooth zoom animation, keeping the graph point under the cursor stable.
    /// </summary>
    /// <remarks>
    /// Keeps the graph point under the cursor stable (Maps / iOS pinch feel).
    /// Accumulates onto the in-flight target so fast flicks feel continuous.
    /// </remarks>
    private void BeginOrRetargetSmoothZoom(Point screenPoint, double factor)
    {
        if (ViewModel is null)
            return;

        // Keep the graph point under the cursor stable (Maps / iOS pinch feel).
        _zoomAnchorScreen = screenPoint;
        _zoomAnchorGraph = GraphViewport.ScreenToGraph(
            screenPoint, ViewModel.PanX, ViewModel.PanY, ViewModel.Zoom);

        // Accumulate onto the in-flight target so fast flicks feel continuous.
        var basis = _zoomAnimating ? _zoomTarget : ViewModel.Zoom;
        _zoomTarget = Math.Clamp(basis * factor, GraphViewport.MinZoom, GraphViewport.MaxZoom);

        if (Math.Abs(_zoomTarget - ViewModel.Zoom) < 0.0002)
            return;

        if (_zoomAnimating)
            return;

        _zoomAnimating = true;
        _lastZoomTick = DateTime.UtcNow;
        _zoomTimer.Start();
    }

    private void OnZoomAnimationTick(object? sender, EventArgs e)
    {
        if (ViewModel is null)
        {
            StopZoomAnimation();
            return;
        }

        var now = DateTime.UtcNow;
        var dt = Math.Min((now - _lastZoomTick).TotalSeconds, 0.05);
        _lastZoomTick = now;

        var nextZoom = GraphViewport.StepToward(ViewModel.Zoom, _zoomTarget, dt);
        var (panX, panY, zoom) = GraphViewport.ZoomTo(
            _zoomAnchorScreen,
            _zoomAnchorGraph,
            ViewModel.PanX,
            ViewModel.PanY,
            nextZoom);

        ViewModel.PanX = panX;
        ViewModel.PanY = panY;
        ViewModel.Zoom = zoom;
        InvalidateVisual();

        if (Math.Abs(ViewModel.Zoom - _zoomTarget) < 0.0002)
        {
            var (finalPanX, finalPanY, finalZoom) = GraphViewport.ZoomTo(
                _zoomAnchorScreen,
                _zoomAnchorGraph,
                ViewModel.PanX,
                ViewModel.PanY,
                _zoomTarget);
            ViewModel.PanX = finalPanX;
            ViewModel.PanY = finalPanY;
            ViewModel.Zoom = finalZoom;
            StopZoomAnimation();
            InvalidateVisual();
        }
    }

    private void StopZoomAnimation()
    {
        _zoomAnimating = false;
        _zoomTimer.Stop();
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

    /// <summary>
    /// Gets whether the user is currently panning the graph view.
    /// </summary>
    internal bool IsViewPanning => _isPanning;

    /// <summary>Forwards a pointer-pressed event to <see cref="OnPointerPressed"/>.</summary>
    /// <param name="e">The pointer event arguments.</param>
    internal void HandlePointerPressed(PointerPressedEventArgs e) => OnPointerPressed(e);

    /// <summary>Forwards a pointer-moved event to <see cref="OnPointerMoved"/>.</summary>
    /// <param name="e">The pointer event arguments.</param>
    internal void HandlePointerMoved(PointerEventArgs e) => OnPointerMoved(e);

    /// <summary>Forwards a pointer-released event to <see cref="OnPointerReleased"/>.</summary>
    /// <param name="e">The pointer event arguments.</param>
    internal void HandlePointerReleased(PointerReleasedEventArgs e) => OnPointerReleased(e);

    /// <inheritdoc />
    /// <remarks>
    /// <para>Handles middle-button or Shift+left-button pan, pin hits, box/zone selection and drag,
    /// and background pan with click-to-select.</para>
    /// <para>
    /// Connection drag: pins are direction-agnostic — any pin under the cursor can start or complete a drag.
    /// The pin where the drag starts is the source; the pin where the user drops is the target.
    /// If <see cref="MainViewModel.IsConnecting"/> is already true, pressing a pin completes the drag
    /// to that pin (box/step or zone). Otherwise, pressing a pin starts a new connection drag from it.
    /// </para>
    /// </remarks>
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

        var pin = HitTestPin(graphPoint);
        if (pin is not null)
        {
            // Pins are direction-agnostic: the pin where the drag starts is the source,
            // and the pin where the user drops is the target. Direction is therefore
            // defined entirely by the drag gesture itself.
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

    /// <inheritdoc />
    /// <remarks>Updates pan, connection drag hover, box/zone drag, resize, and hover cursors.</remarks>
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
            var hoverPin = HitTestPin(graphPoint);
            ViewModel.UpdateConnectionDrag(
                graphPoint.X,
                graphPoint.Y,
                hoverPin?.Box,
                hoverPin?.Step,
                hoverPin?.Side ?? PinSide.Left,
                hoverPin?.Zone);
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
        UpdateErrorPinTooltip(ScreenToGraph(e.GetCurrentPoint(this).Position));
    }

    /// <inheritdoc />
    /// <remarks>Resets the cursor and clears the error-pin tooltip when the pointer leaves the control.</remarks>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ResetCursor();
        ToolTip.SetTip(this, null);
    }

    private const string ErrorPinTooltip =
        "Error / exit — on failure, continue to the connected handler state";

    private const string ZoneErrorPinTooltip =
        "Zone error / exit — any failure inside this zone follows this path";

    private static bool ShowsOwnErrorPin(StateBoxViewModel box) =>
        string.IsNullOrWhiteSpace(box.ZoneId);

    private void UpdateErrorPinTooltip(Point graphPoint)
    {
        if (_isPanning || _isDraggingConnection || _dragBox is not null || _dragZone is not null || _resizeZone is not null)
        {
            ToolTip.SetTip(this, null);
            return;
        }

        var pin = HitTestPin(graphPoint);
        if (pin is not { Side: PinSide.Error })
        {
            ToolTip.SetTip(this, null);
            return;
        }

        ToolTip.SetTip(this, pin.Value.Zone is not null ? ZoneErrorPinTooltip : ErrorPinTooltip);
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

    /// <inheritdoc />
    /// <remarks>Completes or cancels connection drags, finalizes box/zone moves, and ends view pan.</remarks>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isDraggingConnection && ViewModel is not null)
        {
            var graphPoint = ScreenToGraph(e.GetCurrentPoint(this).Position);
            var hoverPin = HitTestPin(graphPoint);
            if (hoverPin is not null)
            {
                if (hoverPin.Value.Zone is not null)
                    ViewModel.TryCompleteConnectionDragToZone(hoverPin.Value.Zone, hoverPin.Value.Side);
                else
                    ViewModel.TryCompleteConnectionDrag(hoverPin.Value.Box!, hoverPin.Value.Step, hoverPin.Value.Side);
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

    /// <inheritdoc />
    /// <remarks>Handles Delete (remove selection) and Escape (cancel connection drag).</remarks>
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

    /// <inheritdoc />
    /// <remarks>
    /// Draws the graph in order: background grid, zones (with pins), then state boxes (with steps and pins).
    /// Applies the current pan and zoom transform from <see cref="ViewModel"/>.
    /// </remarks>
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
        DrawPin(context, zone.GetPinPosition(PinSide.Error), PinSide.Error, IsZonePinHighlighted(zone, PinSide.Error));

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
        if (ShowsOwnErrorPin(box))
            DrawPin(context, box.GetBoxErrorPinPosition(), PinSide.Error, IsPinHighlighted(box, null, PinSide.Error));
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
        if (ShowsOwnErrorPin(box))
            DrawPin(context, box.GetStepErrorPinPosition(step), PinSide.Error, IsPinHighlighted(box, step, PinSide.Error));
    }

    private static Rect GetStepRect(StateBoxViewModel box, double y) =>
        new(box.X + StepInset, y, box.Width - StepInset * 2, 34);

    /// <summary>
    /// Returns whether the given box or step pin should be drawn highlighted during a connection drag.
    /// </summary>
    /// <param name="box">The state box that owns the pin.</param>
    /// <param name="step">The step pin, or <see langword="null"/> for a box-level pin.</param>
    /// <param name="side">Which side of the box or step the pin is on.</param>
    /// <returns>
    /// <see langword="true"/> if this pin is the drag source (where the drag started) or the current
    /// hover target (drop pin under the cursor).
    /// </returns>
    private bool IsPinHighlighted(StateBoxViewModel box, StateStepViewModel? step, PinSide side)
    {
        if (ViewModel is null || !ViewModel.IsConnecting)
            return false;

        // Source pin highlight: the pin where the drag started.
        var isSource = ViewModel.ConnectionSourceZone is null
            && ViewModel.ConnectionSourceBox == box
            && ViewModel.ConnectionSourceStep == step
            && ViewModel.ConnectionSourceSide == side;
        if (isSource)
            return true;

        // Target pin highlight: whichever pin the cursor is currently over.
        return ViewModel.ConnectionHoverZone is null
            && ViewModel.ConnectionHoverBox == box
            && ViewModel.ConnectionHoverStep == step
            && ViewModel.ConnectionHoverSide == side;
    }

    /// <summary>
    /// Returns whether the given zone pin should be drawn highlighted during a connection drag.
    /// </summary>
    /// <param name="zone">The zone that owns the pin.</param>
    /// <param name="side">Which side of the zone the pin is on.</param>
    /// <returns>
    /// <see langword="true"/> if this pin is the drag source or the current hover target.
    /// </returns>
    private bool IsZonePinHighlighted(ZoneViewModel zone, PinSide side)
    {
        if (ViewModel is null || !ViewModel.IsConnecting)
            return false;

        if (ViewModel.ConnectionSourceZone == zone && ViewModel.ConnectionSourceSide == side)
            return true;

        return ViewModel.ConnectionHoverZone == zone && ViewModel.ConnectionHoverSide == side;
    }

    /// <summary>
    /// Draws a single connection pin at the given graph position.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="point">Pin center in graph coordinates.</param>
    /// <param name="side">Pin side (affects error styling).</param>
    /// <param name="isHighlighted">
    /// When <see langword="true"/>, draws the pin enlarged and in the connection-drag accent color.
    /// </param>
    private static void DrawPin(DrawingContext context, (double X, double Y) point, PinSide side, bool isHighlighted)
    {
        var isError = side == PinSide.Error;
        Color fill;
        Color stroke;
        if (isHighlighted)
        {
            fill = Color.FromRgb(255, 140, 0);
            stroke = Color.FromRgb(255, 200, 120);
        }
        else if (isError)
        {
            fill = Color.FromRgb(220, 55, 55);
            stroke = Color.FromRgb(90, 20, 20);
        }
        else
        {
            fill = Color.FromRgb(240, 240, 240);
            stroke = Color.FromRgb(30, 30, 30);
        }

        var radius = isHighlighted ? 7.5 : isError ? 6.5 : 6;
        context.DrawEllipse(
            new SolidColorBrush(fill),
            new Pen(new SolidColorBrush(stroke), isHighlighted || isError ? 2 : 1),
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

    /// <summary>
    /// Returns the topmost pin under the cursor, regardless of pin side or direction.
    /// </summary>
    /// <param name="graphPoint">Point in graph coordinates.</param>
    /// <returns>
    /// The hit pin (box/step or zone), or <see langword="null"/> if no pin is within hit tolerance.
    /// Used for connection drag start and drop; drag start pin is the source, drop pin is the target.
    /// </returns>
    private GraphPin? HitTestPin(Point graphPoint)
    {
        for (var i = ViewModel!.Boxes.Count - 1; i >= 0; i--)
        {
            var box = ViewModel.Boxes[i];
            foreach (var step in box.Steps)
            {
                if (ShowsOwnErrorPin(box))
                {
                    var error = box.GetStepErrorPinPosition(step);
                    if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, error.X, error.Y))
                        return new GraphPin(box, Zone: null, step, Side: PinSide.Error);
                }

                var right = box.GetStepPinPosition(step, PinSide.Right);
                if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, right.X, right.Y))
                    return new GraphPin(box, Zone: null, step, Side: PinSide.Right);

                var bottom = box.GetStepPinPosition(step, PinSide.Bottom);
                if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, bottom.X, bottom.Y))
                    return new GraphPin(box, Zone: null, step, Side: PinSide.Bottom);

                var left = box.GetStepPinPosition(step, PinSide.Left);
                if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, left.X, left.Y))
                    return new GraphPin(box, Zone: null, step, Side: PinSide.Left);

                var top = box.GetStepPinPosition(step, PinSide.Top);
                if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, top.X, top.Y))
                    return new GraphPin(box, Zone: null, step, Side: PinSide.Top);
            }

            if (ShowsOwnErrorPin(box))
            {
                var boxError = box.GetBoxErrorPinPosition();
                if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, boxError.X, boxError.Y))
                    return new GraphPin(box, Zone: null, null, Side: PinSide.Error);
            }

            var boxRight = box.GetBoxPinPosition(PinSide.Right);
            if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, boxRight.X, boxRight.Y))
                return new GraphPin(box, Zone: null, null, Side: PinSide.Right);

            var boxBottom = box.GetBoxPinPosition(PinSide.Bottom);
            if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, boxBottom.X, boxBottom.Y))
                return new GraphPin(box, Zone: null, null, Side: PinSide.Bottom);

            var boxLeft = box.GetBoxPinPosition(PinSide.Left);
            if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, boxLeft.X, boxLeft.Y))
                return new GraphPin(box, Zone: null, null, Side: PinSide.Left);

            var boxTop = box.GetBoxPinPosition(PinSide.Top);
            if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, boxTop.X, boxTop.Y))
                return new GraphPin(box, Zone: null, null, Side: PinSide.Top);
        }

        for (var i = ViewModel.Zones.Count - 1; i >= 0; i--)
        {
            var zone = ViewModel.Zones[i];

            var error = zone.GetPinPosition(PinSide.Error);
            if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, error.X, error.Y))
                return new GraphPin(Box: null, zone, Step: null, Side: PinSide.Error);

            var right = zone.GetPinPosition(PinSide.Right);
            if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, right.X, right.Y))
                return new GraphPin(Box: null, zone, Step: null, Side: PinSide.Right);

            var bottom = zone.GetPinPosition(PinSide.Bottom);
            if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, bottom.X, bottom.Y))
                return new GraphPin(Box: null, zone, Step: null, Side: PinSide.Bottom);

            var left = zone.GetPinPosition(PinSide.Left);
            if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, left.X, left.Y))
                return new GraphPin(Box: null, zone, Step: null, Side: PinSide.Left);

            var top = zone.GetPinPosition(PinSide.Top);
            if (ConnectionRenderHelper.IsNearPin(graphPoint.X, graphPoint.Y, top.X, top.Y))
                return new GraphPin(Box: null, zone, Step: null, Side: PinSide.Top);
        }

        return null;
    }
}

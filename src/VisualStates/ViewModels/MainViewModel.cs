using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualStates.Core.Commands;
using VisualStates.Core;
using VisualStates.Core.Models;
using VisualStates.Services;

namespace VisualStates.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IUndoRedoService _undoRedoService;
    private readonly ICodeGenerationService _codeGenerationService;
    private readonly IFileDialogService _fileDialogService;

    public MainViewModel(
        IProjectService projectService,
        IUndoRedoService undoRedoService,
        ICodeGenerationService codeGenerationService,
        IFileDialogService fileDialogService)
    {
        _projectService = projectService;
        _undoRedoService = undoRedoService;
        _codeGenerationService = codeGenerationService;
        _fileDialogService = fileDialogService;

        _projectService.ProjectChanged += (_, _) => RefreshFromProject();
        _undoRedoService.StateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        };

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SelectedBox) or nameof(SelectedStep) or nameof(SelectedZone))
                HookSelectionEditors();
        };

        RefreshFromProject();
    }

    private void HookSelectionEditors()
    {
        if (SelectedBox is not null)
        {
            SelectedBox.PropertyChanged -= OnEditorPropertyChanged;
            SelectedBox.PropertyChanged += OnEditorPropertyChanged;
        }

        if (SelectedStep is not null)
        {
            SelectedStep.PropertyChanged -= OnEditorPropertyChanged;
            SelectedStep.PropertyChanged += OnEditorPropertyChanged;
        }

        if (SelectedZone is not null)
        {
            SelectedZone.PropertyChanged -= OnEditorPropertyChanged;
            SelectedZone.PropertyChanged += OnEditorPropertyChanged;
        }
    }

    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _projectService.MarkDirty();
        UpdateTitle();
        GeneratedCode = _codeGenerationService.Generate(_projectService.Current);
        GraphRevision++;
    }

    public ObservableCollection<StateBoxViewModel> Boxes { get; } = [];
    public ObservableCollection<ZoneViewModel> Zones { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];
    public ObservableCollection<StateBoxViewModel> SelectedZoneChildBoxes { get; } = [];
    public ObservableCollection<ToolboxItemViewModel> ToolboxItems { get; } =
    [
        new ToolboxItemViewModel("Zone", ToolboxItemKind.Zone, "Add a container zone for grouping states"),
        new ToolboxItemViewModel("State Box", ToolboxItemKind.StateBox, "Add a new state box"),
        new ToolboxItemViewModel("Set Variable", ToolboxItemKind.SetVariable, "Set a variable value"),
        new ToolboxItemViewModel("Call Event", ToolboxItemKind.CallEvent, "Raise an event"),
        new ToolboxItemViewModel("Call Method", ToolboxItemKind.CallMethod, "Invoke a method")
    ];

    [ObservableProperty]
    private string _title = "VisualStates";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private double _panX;

    [ObservableProperty]
    private double _panY;

    [ObservableProperty]
    private StateBoxViewModel? _selectedBox;

    [ObservableProperty]
    private StateStepViewModel? _selectedStep;

    [ObservableProperty]
    private ConnectionViewModel? _selectedConnection;

    [ObservableProperty]
    private ZoneViewModel? _selectedZone;

    [ObservableProperty]
    private string _generatedCode = string.Empty;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private StateBoxViewModel? _connectionSourceBox;

    [ObservableProperty]
    private StateStepViewModel? _connectionSourceStep;

    [ObservableProperty]
    private ZoneViewModel? _connectionSourceZone;

    [ObservableProperty]
    private PinSide _connectionSourceSide = PinSide.Right;

    [ObservableProperty]
    private double _connectionDragEndX;

    [ObservableProperty]
    private double _connectionDragEndY;

    [ObservableProperty]
    private StateBoxViewModel? _connectionHoverBox;

    [ObservableProperty]
    private StateStepViewModel? _connectionHoverStep;

    [ObservableProperty]
    private ZoneViewModel? _connectionHoverZone;

    [ObservableProperty]
    private PinSide _connectionHoverSide;

    [ObservableProperty]
    private ToolboxItemViewModel? _selectedToolboxItem;

    public bool CanUndo => _undoRedoService.CanUndo;
    public bool CanRedo => _undoRedoService.CanRedo;

    [ObservableProperty]
    private int _graphRevision;

    [ObservableProperty]
    private ZoneViewModel? _zoneDropTarget;

    public void NotifyGraphChanged() => GraphRevision++;

    public StateBoxViewModel? FindBox(string id) =>
        Boxes.FirstOrDefault(box => box.Id == id);

    public StateStepViewModel? FindStep(string boxId, string? stepId)
    {
        var box = FindBox(boxId);
        if (box is null || string.IsNullOrWhiteSpace(stepId))
            return null;

        return box.Steps.FirstOrDefault(step => step.Id == stepId);
    }

    [RelayCommand]
    private void NewProject()
    {
        _undoRedoService.Clear();
        _projectService.NewProject();
        StatusText = "Created new project";
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        var path = await _fileDialogService.PickOpenFileAsync("Open State Project", "State Project", "state");
        if (path is null)
            return;

        await _projectService.OpenAsync(path);
        _undoRedoService.Clear();
        StatusText = $"Opened {Path.GetFileName(path)}";
    }

    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(_projectService.CurrentFilePath))
        {
            await SaveProjectAsAsync();
            return;
        }

        await _projectService.SaveAsync();
        UpdateTitle();
        StatusText = "Project saved";
    }

    [RelayCommand]
    private async Task SaveProjectAsAsync()
    {
        var path = await _fileDialogService.PickSaveFileAsync(
            "Save State Project",
            "State Project",
            "state",
            $"{_projectService.Current.Name}.state");

        if (path is null)
            return;

        await _projectService.SaveAsAsync(path);
        UpdateTitle();
        StatusText = $"Saved to {Path.GetFileName(path)}";
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _undoRedoService.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _undoRedoService.Redo();

    [RelayCommand]
    private void AddStateBox()
    {
        var zone = SelectedZone;
        var childCount = zone is null ? 0 : Boxes.Count(b => b.ZoneId == zone.Id);
        var box = new StateBox
        {
            Name = $"State {Boxes.Count + 1}",
            X = zone is null
                ? 120 + Boxes.Count * 40
                : zone.X + 20 + childCount * 24,
            Y = zone is null
                ? 120 + Boxes.Count * 30
                : zone.BodyTop + 20 + childCount * 20,
            ZoneId = zone?.Id,
            HeaderColor = BoxColorPalette.PickNext(Boxes.Select(b => b.Model.HeaderColor))
        };

        ExecuteTracked("Add State Box", () =>
        {
            _projectService.Current.Boxes.Add(box);
            var boxVm = new StateBoxViewModel(box, this);
            Boxes.Add(boxVm);
            SelectBox(boxVm);
            RefreshSelectedZoneChildren();
            NotifyGraphChanged();
            _projectService.MarkDirty();
            UpdateTitle();
        }, () =>
        {
            _projectService.Current.Boxes.Remove(box);
            var vm = Boxes.FirstOrDefault(b => b.Id == box.Id);
            if (vm is not null)
                Boxes.Remove(vm);
            _projectService.MarkDirty();
            UpdateTitle();
        });
    }

    [RelayCommand]
    private void AddStepToSelectedBox(StepKind kind)
    {
        if (SelectedBox is null)
        {
            StatusText = "Select a state box first";
            return;
        }

        var step = CreateStep(kind, SelectedBox.Steps.Count + 1);
        var box = SelectedBox;
        ExecuteTracked($"Add {step.Name}", () =>
        {
            box.AddStep(step);
            var stepVm = box.Steps.Last();
            SelectStep(stepVm);
            GeneratedCode = _codeGenerationService.Generate(_projectService.Current);
            _projectService.MarkDirty();
            UpdateTitle();
        }, () =>
        {
            var stepVm = box.Steps.FirstOrDefault(s => s.Id == step.Id);
            if (stepVm is not null)
                box.RemoveStep(stepVm);
            _projectService.MarkDirty();
            UpdateTitle();
        });
    }

    [RelayCommand]
    private void DeleteStepItem(StateStepViewModel? step)
    {
        if (step is null)
            return;

        RemoveStepFromBox(step.Parent, step);
    }

    [RelayCommand]
    private void AddZone()
    {
        var zone = new Zone
        {
            Name = $"Zone {Zones.Count + 1}",
            X = 80 + Zones.Count * 50,
            Y = 80 + Zones.Count * 40,
            BorderColor = BoxColorPalette.PickNext(Zones.Select(z => z.Model.BorderColor))
        };

        ExecuteTracked("Add Zone", () =>
        {
            _projectService.Current.Zones.Add(zone);
            var zoneVm = new ZoneViewModel(zone, this);
            Zones.Add(zoneVm);
            SelectZone(zoneVm);
            NotifyGraphChanged();
            _projectService.MarkDirty();
            UpdateTitle();
        }, () =>
        {
            _projectService.Current.Zones.Remove(zone);
            var vm = Zones.FirstOrDefault(z => z.Id == zone.Id);
            if (vm is not null)
                Zones.Remove(vm);
            _projectService.MarkDirty();
            UpdateTitle();
        });
    }

    [RelayCommand]
    private void DeleteZone()
    {
        if (SelectedZone is null)
            return;

        var zone = SelectedZone;
        var children = Boxes.Where(b => b.ZoneId == zone.Id).ToList();

        ExecuteTracked("Delete Zone", () =>
        {
            foreach (var child in children)
                child.ZoneId = null;

            _projectService.Current.Zones.Remove(zone.Model);
            Zones.Remove(zone);
            SelectedZone = null;
            RefreshSelectedZoneChildren();
            NotifyGraphChanged();
            _projectService.MarkDirty();
            UpdateTitle();
        }, () =>
        {
            _projectService.Current.Zones.Add(zone.Model);
            Zones.Add(zone);
            foreach (var child in children)
                child.ZoneId = zone.Id;

            _projectService.MarkDirty();
            UpdateTitle();
        });
    }

    [RelayCommand]
    private void RemoveBoxFromZone(StateBoxViewModel? box)
    {
        box ??= SelectedBox;
        if (box is null || box.ZoneId is null)
            return;

        var oldZoneId = box.ZoneId;
        ExecuteTracked("Remove from Zone", () =>
        {
            box.ZoneId = null;
            RefreshSelectedZoneChildren();
            NotifyGraphChanged();
            _projectService.MarkDirty();
            UpdateTitle();
        }, () =>
        {
            box.ZoneId = oldZoneId;
            RefreshSelectedZoneChildren();
            NotifyGraphChanged();
            _projectService.MarkDirty();
            UpdateTitle();
        });
    }

    public ZoneViewModel? FindZone(string? zoneId) =>
        string.IsNullOrWhiteSpace(zoneId) ? null : Zones.FirstOrDefault(z => z.Id == zoneId);

    public ZoneViewModel? SelectedBoxParentZone
    {
        get => SelectedBox?.ParentZone;
        set
        {
            if (SelectedBox is null)
                return;

            var newId = value?.Id;
            if (SelectedBox.ZoneId == newId)
                return;

            SelectedBox.ZoneId = newId;
            RefreshSelectedZoneChildren();
            _projectService.MarkDirty();
            UpdateTitle();
            OnPropertyChanged();
        }
    }

    public void RefreshSelectedZoneChildren()
    {
        SelectedZoneChildBoxes.Clear();
        if (SelectedZone is null)
            return;

        foreach (var box in Boxes.Where(b => b.ZoneId == SelectedZone.Id))
            SelectedZoneChildBoxes.Add(box);
    }

    [RelayCommand]
    private void DeleteSelection()
    {
        if (SelectedConnection is not null)
        {
            DeleteConnection(SelectedConnection);
            return;
        }

        if (SelectedStep is not null && SelectedBox is not null)
        {
            RemoveStepFromBox(SelectedBox, SelectedStep);
            return;
        }

        if (SelectedZone is not null)
        {
            DeleteZone();
            return;
        }

        if (SelectedBox is not null)
            DeleteBox(SelectedBox);
    }

    [RelayCommand]
    private void GenerateCode()
    {
        GeneratedCode = _codeGenerationService.Generate(_projectService.Current);
        StatusText = "Generated C# state machine";
    }

    [RelayCommand]
    private void SelectConnection(ConnectionViewModel? connection)
    {
        foreach (var item in Connections)
            item.IsSelected = item == connection;

        SelectedConnection = connection;
        SelectedStep = null;

        if (connection?.SourceBox is not null)
        {
            foreach (var box in Boxes)
                box.IsSelected = false;
            SelectedBox = null;
        }

        foreach (var zone in Zones)
            zone.IsSelected = false;
        SelectedZone = null;

        NotifyGraphChanged();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectBox(null);
        SelectZone(null);
        SelectConnection(null);
    }

    [RelayCommand]
    private void SelectZone(ZoneViewModel? zone)
    {
        foreach (var item in Zones)
            item.IsSelected = item == zone;

        SelectedZone = zone;
        SelectedBox = null;
        SelectedStep = null;
        SelectedConnection = null;
        foreach (var box in Boxes)
            box.IsSelected = false;
        foreach (var connection in Connections)
            connection.IsSelected = false;

        RefreshSelectedZoneChildren();
        NotifyGraphChanged();
    }

    [RelayCommand]
    private void SelectBox(StateBoxViewModel? box)
    {
        foreach (var item in Boxes)
            item.IsSelected = item == box;

        SelectedBox = box;
        SelectedStep = null;
        SelectedConnection = null;
        foreach (var connection in Connections)
            connection.IsSelected = false;

        foreach (var zone in Zones)
            zone.IsSelected = false;
        SelectedZone = null;

        OnPropertyChanged(nameof(SelectedBoxParentZone));
        NotifyGraphChanged();
    }

    [RelayCommand]
    private void SelectStep(StateStepViewModel? step)
    {
        SelectedStep = step;
        SelectedConnection = null;
        foreach (var connection in Connections)
            connection.IsSelected = false;
        if (step is not null)
        {
            foreach (var box in Boxes)
                box.IsSelected = box == step.Parent;

            SelectedBox = step.Parent;
        }

        foreach (var zone in Zones)
            zone.IsSelected = false;
        SelectedZone = null;

        NotifyGraphChanged();
    }

    [RelayCommand]
    private void BeginConnection()
    {
        // Kept for command binding compatibility; connections are created by dragging pins on the canvas.
    }

    public void StartConnectionDrag(
        StateBoxViewModel box, StateStepViewModel? step, PinSide side, double endX, double endY)
    {
        StartConnectionDragCore(box, step, zone: null, side, endX, endY);
    }

    public void StartConnectionDragFromZone(ZoneViewModel zone, PinSide side, double endX, double endY)
    {
        if (!ZoneFlow.IsOutputSide(side))
            return;

        var exitBox = zone.GetExitBox();
        StartConnectionDragCore(exitBox, step: null, zone, side, endX, endY);
    }

    private void StartConnectionDragCore(
        StateBoxViewModel? box, StateStepViewModel? step, ZoneViewModel? zone,
        PinSide side, double endX, double endY)
    {
        IsConnecting = true;
        ConnectionSourceBox = box;
        ConnectionSourceStep = step;
        ConnectionSourceZone = zone;
        ConnectionSourceSide = side;
        ConnectionDragEndX = endX;
        ConnectionDragEndY = endY;
        ConnectionHoverBox = null;
        ConnectionHoverStep = null;
        ConnectionHoverZone = null;
        ConnectionHoverSide = PinSide.Left;
        StatusText = "Drag to an input pin";
        NotifyGraphChanged();
    }

    public void UpdateConnectionDrag(
        double endX, double endY,
        StateBoxViewModel? hoverBox, StateStepViewModel? hoverStep, PinSide hoverSide,
        ZoneViewModel? hoverZone = null)
    {
        ConnectionDragEndX = endX;
        ConnectionDragEndY = endY;
        ConnectionHoverBox = hoverBox;
        ConnectionHoverStep = hoverStep;
        ConnectionHoverZone = hoverZone;
        ConnectionHoverSide = hoverSide;
        NotifyGraphChanged();
    }

    public bool TryCompleteConnectionDrag(
        StateBoxViewModel targetBox, StateStepViewModel? targetStep, PinSide targetSide)
    {
        return TryCompleteConnectionDragCore(targetBox, targetStep, targetZone: null, targetSide);
    }

    public bool TryCompleteConnectionDragToZone(ZoneViewModel targetZone, PinSide targetSide)
    {
        if (!ZoneFlow.IsInputSide(targetSide))
            return false;

        return TryCompleteConnectionDragCore(
            targetZone.GetEnterBox(), targetStep: null, targetZone, targetSide);
    }

    private bool TryCompleteConnectionDragCore(
        StateBoxViewModel? targetBox, StateStepViewModel? targetStep,
        ZoneViewModel? targetZone, PinSide targetSide)
    {
        if (!IsConnecting)
            return false;

        // Zone-only endpoints are allowed (empty zone); box-less non-zone endpoints are not.
        if (ConnectionSourceZone is null && ConnectionSourceBox is null)
            return false;
        if (targetZone is null && targetBox is null)
            return false;

        if (ConnectionSourceZone is null
            && targetZone is null
            && ConnectionSourceBox == targetBox
            && ConnectionSourceStep == targetStep)
        {
            CancelConnection();
            return false;
        }

        if (ConnectionSourceZone is not null && targetZone is not null
            && ConnectionSourceZone.Id == targetZone.Id)
        {
            CancelConnection();
            return false;
        }

        var sourceStepId = ConnectionSourceZone is not null
            ? (ConnectionSourceBox is null ? null : ResolveExitStepId(ConnectionSourceBox))
            : ConnectionSourceStep?.Id;
        var targetStepId = targetZone is not null
            ? (targetBox is null ? null : ResolveEnterStepId(targetBox))
            : targetStep?.Id;

        var connection = new StateConnection
        {
            SourceBoxId = ConnectionSourceBox?.Id ?? string.Empty,
            SourceStepId = sourceStepId,
            SourceZoneId = ConnectionSourceZone?.Id,
            SourceSide = ConnectionSourceSide,
            TargetBoxId = targetBox?.Id ?? string.Empty,
            TargetStepId = targetStepId,
            TargetZoneId = targetZone?.Id,
            TargetSide = targetSide
        };

        var vm = new ConnectionViewModel(connection, this);
        ExecuteTracked("Add Connection", () =>
        {
            _projectService.Current.Connections.Add(connection);
            Connections.Add(vm);
            _projectService.MarkDirty();
            UpdateTitle();
        }, () =>
        {
            _projectService.Current.Connections.Remove(connection);
            Connections.Remove(vm);
            _projectService.MarkDirty();
            UpdateTitle();
        });

        CancelConnection();
        StatusText = "Connection created";
        return true;
    }

    private static string? ResolveEnterStepId(StateBoxViewModel box) =>
        box.Steps.FirstOrDefault()?.Id;

    private static string? ResolveExitStepId(StateBoxViewModel box) =>
        box.Steps.Count > 0 ? box.Steps[^1].Id : null;

    public void TryCompleteConnection(StateBoxViewModel targetBox, StateStepViewModel? targetStep = null, PinSide targetSide = PinSide.Left)
    {
        TryCompleteConnectionDrag(targetBox, targetStep, targetSide);
    }

    [RelayCommand]
    private void CancelConnection()
    {
        IsConnecting = false;
        ConnectionSourceBox = null;
        ConnectionSourceStep = null;
        ConnectionSourceZone = null;
        ConnectionSourceSide = PinSide.Right;
        ConnectionHoverBox = null;
        ConnectionHoverStep = null;
        ConnectionHoverZone = null;
        ConnectionHoverSide = PinSide.Left;
        StatusText = "Ready";
        NotifyGraphChanged();
    }

    [RelayCommand]
    private void ToolboxAction(ToolboxItemViewModel? item)
    {
        if (item is null)
            return;

        switch (item.Kind)
        {
            case ToolboxItemKind.Zone:
                AddZone();
                break;
            case ToolboxItemKind.StateBox:
                AddStateBox();
                break;
            case ToolboxItemKind.SetVariable:
                AddStepToSelectedBox(StepKind.SetVariable);
                break;
            case ToolboxItemKind.CallEvent:
                AddStepToSelectedBox(StepKind.CallEvent);
                break;
            case ToolboxItemKind.CallMethod:
                AddStepToSelectedBox(StepKind.CallMethod);
                break;
        }
    }

    public void MarkLayoutChanged()
    {
        _projectService.MarkDirty();
        UpdateTitle();
    }

    public void DetachBoxIfOutsideParentZone(StateBoxViewModel box) =>
        ApplyBoxZoneDrop(box);

    public void ApplyBoxZoneDrop(StateBoxViewModel box)
    {
        var centerX = box.X + box.Width / 2;
        var centerY = box.Y + box.GetTotalHeight() / 2;
        var target = FindZoneAtBodyPoint(centerX, centerY);
        var newZoneId = target?.Id;

        if (box.ZoneId == newZoneId)
        {
            ZoneDropTarget = null;
            return;
        }

        box.ZoneId = newZoneId;
        ZoneDropTarget = null;
        RefreshSelectedZoneChildren();
        NotifyGraphChanged();
        _projectService.MarkDirty();
        UpdateTitle();
    }

    public ZoneViewModel? FindZoneAtBodyPoint(double x, double y)
    {
        for (var i = Zones.Count - 1; i >= 0; i--)
        {
            var zone = Zones[i];
            if (zone.ContainsBodyPoint(x, y))
                return zone;
        }

        return null;
    }

    public void UpdateZoneDropTarget(StateBoxViewModel? draggedBox)
    {
        if (draggedBox is null)
        {
            ZoneDropTarget = null;
            return;
        }

        var centerX = draggedBox.X + draggedBox.Width / 2;
        var centerY = draggedBox.Y + draggedBox.GetTotalHeight() / 2;
        var target = FindZoneAtBodyPoint(centerX, centerY);
        ZoneDropTarget = target?.Id == draggedBox.ZoneId ? null : target;
    }

    public void MoveBox(StateBoxViewModel box, double deltaX, double deltaY)
    {
        var oldX = box.X;
        var oldY = box.Y;
        var newX = oldX + deltaX;
        var newY = oldY + deltaY;

        ExecuteTracked("Move Box", () =>
        {
            box.X = newX;
            box.Y = newY;
            _projectService.MarkDirty();
            UpdateTitle();
        }, () =>
        {
            box.X = oldX;
            box.Y = oldY;
            _projectService.MarkDirty();
            UpdateTitle();
        });
    }

    private void DeleteBox(StateBoxViewModel box)
    {
        var removedConnections = Connections
            .Where(c => c.SourceBox == box || c.TargetBox == box)
            .ToList();

        ExecuteTracked("Delete Box", () =>
        {
            foreach (var connection in removedConnections)
            {
                _projectService.Current.Connections.Remove(connection.Model);
                Connections.Remove(connection);
            }

            _projectService.Current.Boxes.Remove(box.Model);
            Boxes.Remove(box);
            SelectedBox = null;
            _projectService.MarkDirty();
            UpdateTitle();
        }, () =>
        {
            _projectService.Current.Boxes.Add(box.Model);
            Boxes.Add(box);
            foreach (var connection in removedConnections)
            {
                _projectService.Current.Connections.Add(connection.Model);
                Connections.Add(connection);
            }
            _projectService.MarkDirty();
            UpdateTitle();
        });
    }

    private void RemoveStepFromBox(StateBoxViewModel box, StateStepViewModel step)
    {
        var removedConnections = Connections
            .Where(c => c.SourceStep == step || c.TargetStep == step)
            .ToList();

        ExecuteTracked("Delete Step", () =>
        {
            foreach (var connection in removedConnections)
            {
                _projectService.Current.Connections.Remove(connection.Model);
                Connections.Remove(connection);
            }

            box.RemoveStep(step);
            SelectedStep = null;
            GeneratedCode = _codeGenerationService.Generate(_projectService.Current);
            _projectService.MarkDirty();
            UpdateTitle();
        }, () =>
        {
            box.AddStep(step.Model);
            foreach (var connection in removedConnections)
            {
                _projectService.Current.Connections.Add(connection.Model);
                Connections.Add(connection);
            }
            _projectService.MarkDirty();
            UpdateTitle();
        });
    }

    private void DeleteConnection(ConnectionViewModel connection)
    {
        ExecuteTracked("Delete Connection", () =>
        {
            _projectService.Current.Connections.Remove(connection.Model);
            Connections.Remove(connection);
            SelectedConnection = null;
            _projectService.MarkDirty();
            UpdateTitle();
        }, () =>
        {
            _projectService.Current.Connections.Add(connection.Model);
            Connections.Add(connection);
            _projectService.MarkDirty();
            UpdateTitle();
        });
    }

    private void RefreshFromProject()
    {
        Boxes.Clear();
        Zones.Clear();
        Connections.Clear();
        SelectedZoneChildBoxes.Clear();

        foreach (var zone in _projectService.Current.Zones)
            Zones.Add(new ZoneViewModel(zone, this));

        foreach (var box in _projectService.Current.Boxes)
            Boxes.Add(new StateBoxViewModel(box, this));

        foreach (var connection in _projectService.Current.Connections)
            Connections.Add(new ConnectionViewModel(connection, this));

        GeneratedCode = _codeGenerationService.Generate(_projectService.Current);
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        var name = _projectService.Current.Name;
        var dirty = _projectService.IsDirty ? " *" : string.Empty;
        var file = _projectService.CurrentFilePath is null
            ? string.Empty
            : $" - {Path.GetFileName(_projectService.CurrentFilePath)}";
        Title = $"{name}{dirty}{file} - VisualStates";
    }

    private void ExecuteTracked(string description, Action execute, Action undo)
    {
        _undoRedoService.Execute(new ActionCommand(description, execute, undo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private static StateStep CreateStep(StepKind kind, int index) => new()
    {
        Kind = kind,
        Name = kind switch
        {
            StepKind.SetVariable => $"Set Variable {index}",
            StepKind.CallEvent => $"Call Event {index}",
            StepKind.CallMethod => $"Call Method {index}",
            _ => $"Step {index}"
        },
        TargetName = kind == StepKind.SetVariable ? "MyVariable" : null,
        Expression = kind == StepKind.SetVariable ? "\"value\"" : null,
        EventName = kind == StepKind.CallEvent ? "OnEvent" : null,
        MethodName = kind == StepKind.CallMethod ? "Execute" : null
    };
}

public enum ToolboxItemKind
{
    Zone,
    StateBox,
    SetVariable,
    CallEvent,
    CallMethod
}

public sealed class ToolboxItemViewModel
{
    public ToolboxItemViewModel(string title, ToolboxItemKind kind, string description)
    {
        Title = title;
        Kind = kind;
        Description = description;
    }

    public string Title { get; }
    public ToolboxItemKind Kind { get; }
    public string Description { get; }
}

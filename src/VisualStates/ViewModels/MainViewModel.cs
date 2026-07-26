using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualStates.Core.Commands;
using VisualStates.Core;
using VisualStates.Core.Models;
using VisualStates.Runtime;
using VisualStates.Services;

namespace VisualStates.ViewModels;

/// <summary>
/// Root editor view-model for the VisualStates canvas. Coordinates project I/O, graph selection,
/// toolbox actions, the connection drag lifecycle, undo/redo, C# code generation, and stepped
/// execution preview against the current <see cref="StateProject"/>.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IUndoRedoService _undoRedoService;
    private readonly ICodeGenerationService _codeGenerationService;
    private readonly IFileDialogService _fileDialogService;
    private readonly StateMachineExecutor _executor = new();
    private CancellationTokenSource? _executionCts;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class and subscribes to
    /// project, undo/redo, and selection change notifications.
    /// </summary>
    /// <param name="projectService">Service for loading, saving, and mutating the current project.</param>
    /// <param name="undoRedoService">Service that tracks reversible editor commands.</param>
    /// <param name="codeGenerationService">Service that generates C# from the current project model.</param>
    /// <param name="fileDialogService">Service used for open/save file dialogs.</param>
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

    /// <summary>
    /// Subscribes to property changes on the currently selected box, step, or zone so inline
    /// edits mark the project dirty and refresh generated code.
    /// </summary>
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

    /// <summary>
    /// Handles inline property edits on the selected graph item by marking the project dirty,
    /// updating the window title, regenerating code, and bumping the graph revision.
    /// </summary>
    /// <param name="sender">The view-model whose property changed.</param>
    /// <param name="e">Details about the property change.</param>
    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _projectService.MarkDirty();
        UpdateTitle();
        GeneratedCode = _codeGenerationService.Generate(_projectService.Current);
        GraphRevision++;
    }

    /// <summary>
    /// Gets the state boxes displayed on the canvas.
    /// </summary>
    public ObservableCollection<StateBoxViewModel> Boxes { get; } = [];

    /// <summary>
    /// Gets the container zones displayed on the canvas.
    /// </summary>
    public ObservableCollection<ZoneViewModel> Zones { get; } = [];

    /// <summary>
    /// Gets the connections between pins on boxes, steps, and zones.
    /// </summary>
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    /// <summary>
    /// Gets the state boxes that belong to <see cref="SelectedZone"/>.
    /// </summary>
    public ObservableCollection<StateBoxViewModel> SelectedZoneChildBoxes { get; } = [];

    /// <summary>
    /// Gets the toolbox entries available for adding zones, boxes, and step kinds.
    /// </summary>
    public ObservableCollection<ToolboxItemViewModel> ToolboxItems { get; } =
    [
        new ToolboxItemViewModel("Zone", ToolboxItemKind.Zone, "Add a container zone for grouping states"),
        new ToolboxItemViewModel("State Box", ToolboxItemKind.StateBox, "Add a new state box"),
        new ToolboxItemViewModel("Set Variable", ToolboxItemKind.SetVariable, "Set a variable value"),
        new ToolboxItemViewModel("Call Event", ToolboxItemKind.CallEvent, "Raise an event"),
        new ToolboxItemViewModel("Call Method", ToolboxItemKind.CallMethod, "Invoke a method")
    ];

    /// <summary>
    /// Backing field for <see cref="Title"/>, the window title including project name, dirty flag, and file name.
    /// </summary>
    [ObservableProperty]
    private string _title = "VisualStates";

    /// <summary>
    /// Backing field for <see cref="StatusText"/>, the short status message shown in the editor chrome.
    /// </summary>
    [ObservableProperty]
    private string _statusText = "Ready";

    /// <summary>
    /// Backing field for <see cref="Zoom"/>, the canvas zoom factor.
    /// </summary>
    [ObservableProperty]
    private double _zoom = 1.0;

    /// <summary>
    /// Backing field for <see cref="PanX"/>, the horizontal canvas pan offset.
    /// </summary>
    [ObservableProperty]
    private double _panX;

    /// <summary>
    /// Backing field for <see cref="PanY"/>, the vertical canvas pan offset.
    /// </summary>
    [ObservableProperty]
    private double _panY;

    /// <summary>
    /// Backing field for <see cref="SelectedBox"/>, the currently selected state box, if any.
    /// </summary>
    [ObservableProperty]
    private StateBoxViewModel? _selectedBox;

    /// <summary>
    /// Backing field for <see cref="SelectedStep"/>, the currently selected step within a box, if any.
    /// </summary>
    [ObservableProperty]
    private StateStepViewModel? _selectedStep;

    /// <summary>
    /// Backing field for <see cref="SelectedConnection"/>, the currently selected connection, if any.
    /// </summary>
    [ObservableProperty]
    private ConnectionViewModel? _selectedConnection;

    /// <summary>
    /// Backing field for <see cref="SelectedZone"/>, the currently selected zone, if any.
    /// </summary>
    [ObservableProperty]
    private ZoneViewModel? _selectedZone;

    /// <summary>
    /// Backing field for <see cref="GeneratedCode"/>, the latest generated C# state machine source.
    /// </summary>
    [ObservableProperty]
    private string _generatedCode = string.Empty;

    /// <summary>
    /// Backing field for <see cref="IsExecuting"/>, whether a stepped execution preview is running.
    /// </summary>
    [ObservableProperty]
    private bool _isExecuting;

    /// <summary>
    /// Backing field for <see cref="ExecutingBox"/>, the box currently highlighted during execution preview.
    /// </summary>
    [ObservableProperty]
    private StateBoxViewModel? _executingBox;

    /// <summary>
    /// Backing field for <see cref="ExecutingStep"/>, the step currently highlighted during execution preview.
    /// </summary>
    [ObservableProperty]
    private StateStepViewModel? _executingStep;

    /// <summary>
    /// Backing field for <see cref="ExecutingConnection"/>, the connection currently highlighted during execution preview.
    /// </summary>
    [ObservableProperty]
    private ConnectionViewModel? _executingConnection;

    /// <summary>
    /// Backing field for <see cref="IsConnecting"/>, whether a connection drag is in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isConnecting;

    /// <summary>
    /// Backing field for <see cref="ConnectionSourceBox"/>, the source box for an in-progress connection drag.
    /// </summary>
    [ObservableProperty]
    private StateBoxViewModel? _connectionSourceBox;

    /// <summary>
    /// Backing field for <see cref="ConnectionSourceStep"/>, the source step for an in-progress connection drag.
    /// </summary>
    [ObservableProperty]
    private StateStepViewModel? _connectionSourceStep;

    /// <summary>
    /// Backing field for <see cref="ConnectionSourceZone"/>, the source zone for an in-progress connection drag.
    /// </summary>
    [ObservableProperty]
    private ZoneViewModel? _connectionSourceZone;

    /// <summary>
    /// Backing field for <see cref="ConnectionSourceSide"/>, the pin side where the connection drag started.
    /// </summary>
    [ObservableProperty]
    private PinSide _connectionSourceSide = PinSide.Right;

    /// <summary>
    /// Backing field for <see cref="ConnectionDragEndX"/>, the canvas X coordinate of the connection rubber-band end.
    /// </summary>
    [ObservableProperty]
    private double _connectionDragEndX;

    /// <summary>
    /// Backing field for <see cref="ConnectionDragEndY"/>, the canvas Y coordinate of the connection rubber-band end.
    /// </summary>
    [ObservableProperty]
    private double _connectionDragEndY;

    /// <summary>
    /// Backing field for <see cref="ConnectionHoverBox"/>, the box under the pointer during a connection drag.
    /// </summary>
    [ObservableProperty]
    private StateBoxViewModel? _connectionHoverBox;

    /// <summary>
    /// Backing field for <see cref="ConnectionHoverStep"/>, the step under the pointer during a connection drag.
    /// </summary>
    [ObservableProperty]
    private StateStepViewModel? _connectionHoverStep;

    /// <summary>
    /// Backing field for <see cref="ConnectionHoverZone"/>, the zone under the pointer during a connection drag.
    /// </summary>
    [ObservableProperty]
    private ZoneViewModel? _connectionHoverZone;

    /// <summary>
    /// Backing field for <see cref="ConnectionHoverSide"/>, the pin side under the pointer during a connection drag.
    /// </summary>
    [ObservableProperty]
    private PinSide _connectionHoverSide;

    /// <summary>
    /// Backing field for <see cref="SelectedToolboxItem"/>, the toolbox item highlighted or chosen by the user.
    /// </summary>
    [ObservableProperty]
    private ToolboxItemViewModel? _selectedToolboxItem;

    /// <summary>
    /// Gets a value indicating whether an undo operation is available.
    /// </summary>
    public bool CanUndo => _undoRedoService.CanUndo;

    /// <summary>
    /// Gets a value indicating whether a redo operation is available.
    /// </summary>
    public bool CanRedo => _undoRedoService.CanRedo;

    /// <summary>
    /// Backing field for <see cref="GraphRevision"/>, a monotonically increasing counter used to invalidate graph rendering.
    /// </summary>
    [ObservableProperty]
    private int _graphRevision;

    /// <summary>
    /// Backing field for <see cref="ZoneDropTarget"/>, the zone highlighted as a drop target while dragging a box.
    /// </summary>
    [ObservableProperty]
    private ZoneViewModel? _zoneDropTarget;

    /// <summary>
    /// Increments <see cref="GraphRevision"/> so views bound to the graph re-render.
    /// </summary>
    public void NotifyGraphChanged() => GraphRevision++;

    /// <summary>
    /// Finds a state box view-model by its identifier.
    /// </summary>
    /// <param name="id">The box identifier to match.</param>
    /// <returns>The matching <see cref="StateBoxViewModel"/>, or <see langword="null"/> if none exists.</returns>
    public StateBoxViewModel? FindBox(string id) =>
        Boxes.FirstOrDefault(box => box.Id == id);

    /// <summary>
    /// Finds a step within a box by box and step identifiers.
    /// </summary>
    /// <param name="boxId">The parent box identifier.</param>
    /// <param name="stepId">The step identifier, or <see langword="null"/> or whitespace to skip lookup.</param>
    /// <returns>The matching <see cref="StateStepViewModel"/>, or <see langword="null"/> if not found.</returns>
    public StateStepViewModel? FindStep(string boxId, string? stepId)
    {
        var box = FindBox(boxId);
        if (box is null || string.IsNullOrWhiteSpace(stepId))
            return null;

        return box.Steps.FirstOrDefault(step => step.Id == stepId);
    }

    /// <summary>
    /// Command that creates a blank project and clears the undo/redo stack.
    /// </summary>
    [RelayCommand]
    private void NewProject()
    {
        _undoRedoService.Clear();
        _projectService.NewProject();
        StatusText = "Created new project";
    }

    /// <summary>
    /// Command that opens a project file selected through the file dialog.
    /// </summary>
    /// <returns>A task that completes when the open operation finishes or is cancelled.</returns>
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

    /// <summary>
    /// Command that saves the current project to its existing file path, or prompts for a path if none is set.
    /// </summary>
    /// <returns>A task that completes when the save operation finishes.</returns>
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

    /// <summary>
    /// Command that saves the current project to a new file path chosen through the file dialog.
    /// </summary>
    /// <returns>A task that completes when the save operation finishes or is cancelled.</returns>
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

    /// <summary>
    /// Command that undoes the most recent tracked editor action.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _undoRedoService.Undo();

    /// <summary>
    /// Command that redoes the most recently undone editor action.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _undoRedoService.Redo();

    /// <summary>
    /// Makes <paramref name="box"/> the sole main entry point, clearing the flag on every other box.
    /// </summary>
    /// <param name="box">The state box to promote as entry.</param>
    public void SetAsEntryPoint(StateBoxViewModel box)
    {
        if (box.IsEntry && Boxes.Count(b => b.IsEntry) == 1)
            return;

        var previous = Boxes.Where(b => b.IsEntry).Select(b => b.Id).ToList();
        ExecuteTracked("Set Main Entry Point", () =>
        {
            ApplyEntryPoint(box);
            StatusText = $"{box.Name} set as main entry point";
            _projectService.MarkDirty();
            UpdateTitle();
            NotifyGraphChanged();
        }, () =>
        {
            foreach (var candidate in Boxes)
                candidate.SetIsEntryCore(previous.Contains(candidate.Id));
            _projectService.MarkDirty();
            UpdateTitle();
            NotifyGraphChanged();
        });
    }

    /// <summary>
    /// Command wrapper for <see cref="SetAsEntryPoint"/> used by context menus and bindings.
    /// </summary>
    /// <param name="box">The state box to promote, or <see langword="null"/> to no-op.</param>
    [RelayCommand]
    private void SetAsMainEntryPoint(StateBoxViewModel? box)
    {
        if (box is null)
            return;

        SetAsEntryPoint(box);
    }

    /// <summary>
    /// Clears entry flags on all boxes, then marks <paramref name="box"/> as the entry point.
    /// </summary>
    private void ApplyEntryPoint(StateBoxViewModel box)
    {
        foreach (var candidate in Boxes)
            candidate.SetIsEntryCore(candidate == box);
    }

    /// <summary>
    /// Command that adds a new state box, optionally inside <see cref="SelectedZone"/>.
    /// </summary>
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

    /// <summary>
    /// Command that appends a step of the given kind to <see cref="SelectedBox"/>.
    /// </summary>
    /// <param name="kind">The kind of step to create.</param>
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

    /// <summary>
    /// Command that removes the specified step from its parent box.
    /// </summary>
    /// <param name="step">The step to delete, or <see langword="null"/> to no-op.</param>
    [RelayCommand]
    private void DeleteStepItem(StateStepViewModel? step)
    {
        if (step is null)
            return;

        RemoveStepFromBox(step.Parent, step);
    }

    /// <summary>
    /// Command that adds a new zone to the canvas.
    /// </summary>
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

    /// <summary>
    /// Command that deletes <see cref="SelectedZone"/> and unparents its child boxes.
    /// </summary>
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

    /// <summary>
    /// Command that removes a box from its parent zone without deleting the box.
    /// </summary>
    /// <param name="box">The box to unparent, or <see langword="null"/> to use <see cref="SelectedBox"/>.</param>
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

    /// <summary>
    /// Finds a zone view-model by its identifier.
    /// </summary>
    /// <param name="zoneId">The zone identifier, or <see langword="null"/> or whitespace to skip lookup.</param>
    /// <returns>The matching <see cref="ZoneViewModel"/>, or <see langword="null"/> if not found.</returns>
    public ZoneViewModel? FindZone(string? zoneId) =>
        string.IsNullOrWhiteSpace(zoneId) ? null : Zones.FirstOrDefault(z => z.Id == zoneId);

    /// <summary>
    /// Gets or sets the parent zone of <see cref="SelectedBox"/> via the property editor.
    /// Assigning a zone removes per-element error connections when the box becomes zoned.
    /// </summary>
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
            if (newId is not null)
                RemoveErrorConnectionsFromBox(SelectedBox);

            RefreshSelectedZoneChildren();
            NotifyGraphChanged();
            _projectService.MarkDirty();
            UpdateTitle();
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Rebuilds <see cref="SelectedZoneChildBoxes"/> from boxes whose <see cref="StateBoxViewModel.ZoneId"/>
    /// matches <see cref="SelectedZone"/>.
    /// </summary>
    public void RefreshSelectedZoneChildren()
    {
        SelectedZoneChildBoxes.Clear();
        if (SelectedZone is null)
            return;

        foreach (var box in Boxes.Where(b => b.ZoneId == SelectedZone.Id))
            SelectedZoneChildBoxes.Add(box);
    }

    /// <summary>
    /// Command that deletes the current selection: connection, step, zone, or box (in that priority order).
    /// </summary>
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

    /// <summary>
    /// Command that regenerates <see cref="GeneratedCode"/> from the current project model.
    /// </summary>
    [RelayCommand]
    private void GenerateCode()
    {
        GeneratedCode = _codeGenerationService.Generate(_projectService.Current);
        StatusText = "Generated C# state machine";
    }

    /// <summary>
    /// Whether <see cref="ExecuteGraphCommand"/> can start a new stepped execution preview.
    /// </summary>
    private bool CanExecuteGraph() => !IsExecuting;

    /// <summary>
    /// Whether <see cref="StopExecutionCommand"/> can cancel the current execution preview.
    /// </summary>
    private bool CanStopExecution() => IsExecuting;

    /// <summary>
    /// Notifies execute/stop command availability when <see cref="IsExecuting"/> changes.
    /// </summary>
    /// <param name="value">New executing flag.</param>
    partial void OnIsExecutingChanged(bool value)
    {
        ExecuteGraphCommand.NotifyCanExecuteChanged();
        StopExecutionCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Command that walks the planned happy-path order one step at a time with a one-second delay,
    /// highlighting the active box, step, and connecting wire.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteGraph))]
    private async Task ExecuteGraphAsync()
    {
        var plan = _executor.GetExecutionPlan(_projectService.Current);
        if (plan.Count == 0)
        {
            StatusText = "Nothing to execute";
            return;
        }

        _executionCts?.Cancel();
        _executionCts?.Dispose();
        _executionCts = new CancellationTokenSource();
        var token = _executionCts.Token;

        IsExecuting = true;
        StatusText = $"Executing 0/{plan.Count}…";

        try
        {
            ExecutionPlanItem? previous = null;
            for (var i = 0; i < plan.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                var item = plan[i];
                ExecutingBox = FindBox(item.BoxId);
                ExecutingStep = item.StepId is null ? null : FindStep(item.BoxId, item.StepId);
                ExecutingConnection = previous is { } prev
                    ? FindConnectionAlongPath(prev, item)
                    : null;

                var label = ExecutingStep?.KindLabel ?? ExecutingBox?.Name ?? item.BoxId;
                StatusText = $"Executing {i + 1}/{plan.Count}: {label}";

                await Task.Delay(TimeSpan.FromSeconds(1), token);
                previous = item;
            }

            StatusText = "Execution complete";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Execution stopped";
        }
        finally
        {
            ClearExecutionHighlight();
            IsExecuting = false;
            _executionCts?.Dispose();
            _executionCts = null;
        }
    }

    /// <summary>
    /// Command that cancels an in-progress stepped execution preview.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopExecution))]
    private void StopExecution()
    {
        _executionCts?.Cancel();
    }

    /// <summary>
    /// Clears execution-preview highlight references.
    /// </summary>
    private void ClearExecutionHighlight()
    {
        ExecutingBox = null;
        ExecutingStep = null;
        ExecutingConnection = null;
    }

    /// <summary>
    /// Finds a non-error connection that links <paramref name="from"/> to <paramref name="to"/>
    /// by box and optional step ids.
    /// </summary>
    private ConnectionViewModel? FindConnectionAlongPath(ExecutionPlanItem from, ExecutionPlanItem to)
    {
        foreach (var connection in Connections)
        {
            if (connection.IsError)
                continue;

            if (!EndpointMatches(connection.Model.SourceBoxId, connection.Model.SourceStepId, from))
                continue;
            if (!EndpointMatches(connection.Model.TargetBoxId, connection.Model.TargetStepId, to))
                continue;

            return connection;
        }

        return null;
    }

    /// <summary>
    /// Returns whether a connection endpoint matches a plan item, allowing blank step ids
    /// to match any step on that box (box-level pins).
    /// </summary>
    private static bool EndpointMatches(string boxId, string? stepId, ExecutionPlanItem item)
    {
        if (boxId != item.BoxId)
            return false;

        if (string.IsNullOrWhiteSpace(stepId))
            return true;

        return stepId == item.StepId;
    }

    /// <summary>
    /// Command that selects a connection and clears competing box, step, and zone selection.
    /// </summary>
    /// <param name="connection">The connection to select, or <see langword="null"/> to clear connection selection.</param>
    [RelayCommand]
    private void SelectConnection(ConnectionViewModel? connection)
    {
        foreach (var item in Connections.ToList())
            item.IsSelected = item == connection;

        SelectedConnection = connection;
        SelectedStep = null;

        if (connection?.SourceBox is not null)
        {
            foreach (var box in Boxes.ToList())
                box.IsSelected = false;
            SelectedBox = null;
        }

        foreach (var zone in Zones.ToList())
            zone.IsSelected = false;
        SelectedZone = null;

        NotifyGraphChanged();
    }

    /// <summary>
    /// Command that clears box, zone, and connection selection.
    /// </summary>
    [RelayCommand]
    private void ClearSelection()
    {
        SelectBox(null);
        SelectZone(null);
        SelectConnection(null);
    }

    /// <summary>
    /// Command that selects a zone and clears competing box, step, and connection selection.
    /// </summary>
    /// <param name="zone">The zone to select, or <see langword="null"/> to clear zone selection.</param>
    [RelayCommand]
    private void SelectZone(ZoneViewModel? zone)
    {
        foreach (var item in Zones.ToList())
            item.IsSelected = item == zone;

        SelectedZone = zone;
        SelectedBox = null;
        SelectedStep = null;
        SelectedConnection = null;
        foreach (var box in Boxes.ToList())
            box.IsSelected = false;
        foreach (var connection in Connections.ToList())
            connection.IsSelected = false;

        RefreshSelectedZoneChildren();
        NotifyGraphChanged();
    }

    /// <summary>
    /// Command that selects a state box and clears competing step, connection, and zone selection.
    /// </summary>
    /// <param name="box">The box to select, or <see langword="null"/> to clear box selection.</param>
    [RelayCommand]
    private void SelectBox(StateBoxViewModel? box)
    {
        foreach (var item in Boxes.ToList())
            item.IsSelected = item == box;

        SelectedBox = box;
        SelectedStep = null;
        SelectedConnection = null;
        foreach (var connection in Connections.ToList())
            connection.IsSelected = false;

        foreach (var zone in Zones.ToList())
            zone.IsSelected = false;
        SelectedZone = null;

        OnPropertyChanged(nameof(SelectedBoxParentZone));
        NotifyGraphChanged();
    }

    /// <summary>
    /// Command that selects a step within its parent box and clears connection and zone selection.
    /// </summary>
    /// <param name="step">The step to select, or <see langword="null"/> to clear step selection only.</param>
    [RelayCommand]
    private void SelectStep(StateStepViewModel? step)
    {
        SelectedStep = step;
        SelectedConnection = null;
        foreach (var connection in Connections.ToList())
            connection.IsSelected = false;
        if (step is not null)
        {
            foreach (var box in Boxes.ToList())
                box.IsSelected = box == step.Parent;

            SelectedBox = step.Parent;
        }

        foreach (var zone in Zones.ToList())
            zone.IsSelected = false;
        SelectedZone = null;

        NotifyGraphChanged();
    }

    /// <summary>
    /// Command retained for binding compatibility; connections are created by dragging pins on the canvas.
    /// </summary>
    [RelayCommand]
    private void BeginConnection()
    {
        // Kept for command binding compatibility; connections are created by dragging pins on the canvas.
    }

    /// <summary>
    /// Starts a connection drag from a box or step pin. Pins are direction-agnostic: the pin where
    /// the drag starts becomes the connection source.
    /// </summary>
    /// <param name="box">The box that owns the source pin.</param>
    /// <param name="step">The source step pin, or <see langword="null"/> for a box-level pin.</param>
    /// <param name="side">The pin side where the drag started.</param>
    /// <param name="endX">Initial rubber-band end X in canvas coordinates.</param>
    /// <param name="endY">Initial rubber-band end Y in canvas coordinates.</param>
    public void StartConnectionDrag(
        StateBoxViewModel box, StateStepViewModel? step, PinSide side, double endX, double endY)
    {
        StartConnectionDragCore(box, step, zone: null, side, endX, endY);
    }

    /// <summary>
    /// Starts a connection drag from a zone exit pin. Pins are direction-agnostic: the pin where
    /// the drag starts becomes the connection source.
    /// </summary>
    /// <param name="zone">The zone that owns the source exit pin.</param>
    /// <param name="side">The pin side where the drag started.</param>
    /// <param name="endX">Initial rubber-band end X in canvas coordinates.</param>
    /// <param name="endY">Initial rubber-band end Y in canvas coordinates.</param>
    public void StartConnectionDragFromZone(ZoneViewModel zone, PinSide side, double endX, double endY)
    {
        var exitBox = zone.GetExitBox();
        StartConnectionDragCore(exitBox, step: null, zone, side, endX, endY);
    }

    /// <summary>
    /// Initializes connection-drag state shared by box/step and zone entry points.
    /// </summary>
    /// <param name="box">The source box, if any.</param>
    /// <param name="step">The source step, if any.</param>
    /// <param name="zone">The source zone, if the drag started on a zone pin.</param>
    /// <param name="side">The pin side where the drag started.</param>
    /// <param name="endX">Initial rubber-band end X in canvas coordinates.</param>
    /// <param name="endY">Initial rubber-band end Y in canvas coordinates.</param>
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
        StatusText = side == PinSide.Error
            ? "Drag error / exit pin to another pin"
            : "Drop on another pin to connect";
        NotifyGraphChanged();
    }

    /// <summary>
    /// Updates the rubber-band end point and hover target while a connection drag is active.
    /// </summary>
    /// <param name="endX">Current rubber-band end X in canvas coordinates.</param>
    /// <param name="endY">Current rubber-band end Y in canvas coordinates.</param>
    /// <param name="hoverBox">The box under the pointer, if any.</param>
    /// <param name="hoverStep">The step under the pointer, if any.</param>
    /// <param name="hoverSide">The pin side under the pointer.</param>
    /// <param name="hoverZone">The zone under the pointer, if any.</param>
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

    /// <summary>
    /// Completes a connection drag onto a box or step pin. Pins are direction-agnostic: the pin
    /// where the user drops becomes the connection target.
    /// </summary>
    /// <param name="targetBox">The box that owns the target pin.</param>
    /// <param name="targetStep">The target step pin, or <see langword="null"/> for a box-level pin.</param>
    /// <param name="targetSide">The pin side where the drag ended.</param>
    /// <returns><see langword="true"/> if a connection was created; otherwise <see langword="false"/>.</returns>
    public bool TryCompleteConnectionDrag(
        StateBoxViewModel targetBox, StateStepViewModel? targetStep, PinSide targetSide)
    {
        return TryCompleteConnectionDragCore(targetBox, targetStep, targetZone: null, targetSide);
    }

    /// <summary>
    /// Completes a connection drag onto a zone enter pin. Pins are direction-agnostic: the pin
    /// where the user drops becomes the connection target.
    /// </summary>
    /// <param name="targetZone">The zone that owns the target enter pin.</param>
    /// <param name="targetSide">The pin side where the drag ended.</param>
    /// <returns><see langword="true"/> if a connection was created; otherwise <see langword="false"/>.</returns>
    public bool TryCompleteConnectionDragToZone(ZoneViewModel targetZone, PinSide targetSide)
    {
        return TryCompleteConnectionDragCore(
            targetZone.GetEnterBox(), targetStep: null, targetZone, targetSide);
    }

    /// <summary>
    /// Validates source and target endpoints, creates a tracked connection, and resets drag state.
    /// Self-connections on the same pin are cancelled without creating a link.
    /// </summary>
    /// <param name="targetBox">The target box, if any.</param>
    /// <param name="targetStep">The target step, if any.</param>
    /// <param name="targetZone">The target zone, if the drag ended on a zone pin.</param>
    /// <param name="targetSide">The pin side where the drag ended.</param>
    /// <returns><see langword="true"/> if a connection was created; otherwise <see langword="false"/>.</returns>
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
            TargetSide = targetSide,
            IsError = ConnectionSourceSide == PinSide.Error
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

    /// <summary>
    /// Resolves the enter step identifier for a zone target as the first step in the box, if any.
    /// </summary>
    /// <param name="box">The target box associated with a zone enter pin.</param>
    /// <returns>The first step identifier, or <see langword="null"/> when the box has no steps.</returns>
    private static string? ResolveEnterStepId(StateBoxViewModel box) =>
        box.Steps.FirstOrDefault()?.Id;

    /// <summary>
    /// Resolves the exit step identifier for a zone source as the last step in the box, if any.
    /// </summary>
    /// <param name="box">The source box associated with a zone exit pin.</param>
    /// <returns>The last step identifier, or <see langword="null"/> when the box has no steps.</returns>
    private static string? ResolveExitStepId(StateBoxViewModel box) =>
        box.Steps.Count > 0 ? box.Steps[^1].Id : null;

    /// <summary>
    /// Legacy completion entry point that delegates to <see cref="TryCompleteConnectionDrag"/>.
    /// </summary>
    /// <param name="targetBox">The box that owns the target pin.</param>
    /// <param name="targetStep">The target step pin, or <see langword="null"/> for a box-level pin.</param>
    /// <param name="targetSide">The pin side where the drag ended.</param>
    public void TryCompleteConnection(StateBoxViewModel targetBox, StateStepViewModel? targetStep = null, PinSide targetSide = PinSide.Left)
    {
        TryCompleteConnectionDrag(targetBox, targetStep, targetSide);
    }

    /// <summary>
    /// Command that cancels an in-progress connection drag and clears hover and source state.
    /// </summary>
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

    /// <summary>
    /// Command that executes the action associated with a toolbox item.
    /// </summary>
    /// <param name="item">The toolbox item to act on, or <see langword="null"/> to no-op.</param>
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

    /// <summary>
    /// Marks the project dirty and refreshes the window title after a layout-only change.
    /// </summary>
    public void MarkLayoutChanged()
    {
        _projectService.MarkDirty();
        UpdateTitle();
    }

    /// <summary>
    /// Applies zone membership after a box drag ends. Delegates to <see cref="ApplyBoxZoneDrop"/>.
    /// </summary>
    /// <param name="box">The box whose zone membership should be reconciled.</param>
    public void DetachBoxIfOutsideParentZone(StateBoxViewModel box) =>
        ApplyBoxZoneDrop(box);

    /// <summary>
    /// Assigns or clears a box's parent zone based on whether its center lies inside a zone body.
    /// Drops per-element error wires when the box becomes zoned.
    /// </summary>
    /// <param name="box">The box being dropped or moved.</param>
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

        // Zone owns error handling for its children — drop per-element error wires.
        if (newZoneId is not null)
            RemoveErrorConnectionsFromBox(box);

        ZoneDropTarget = null;
        RefreshSelectedZoneChildren();
        NotifyGraphChanged();
        _projectService.MarkDirty();
        UpdateTitle();
    }

    /// <summary>
    /// Removes error connections that originate from individual steps on a box once the box is zoned.
    /// </summary>
    /// <param name="box">The box whose per-element error wires should be removed.</param>
    private void RemoveErrorConnectionsFromBox(StateBoxViewModel box)
    {
        var stepIds = box.Steps.Select(s => s.Id).ToHashSet();
        var toRemove = Connections
            .Where(c =>
                c.IsError
                && string.IsNullOrWhiteSpace(c.Model.SourceZoneId)
                && c.Model.SourceBoxId == box.Id
                && (c.Model.SourceStepId is null || stepIds.Contains(c.Model.SourceStepId)))
            .ToList();

        foreach (var connection in toRemove)
        {
            _projectService.Current.Connections.Remove(connection.Model);
            Connections.Remove(connection);
        }
    }

    /// <summary>
    /// Finds the topmost zone whose body contains the given canvas point.
    /// </summary>
    /// <param name="x">The X coordinate in canvas space.</param>
    /// <param name="y">The Y coordinate in canvas space.</param>
    /// <returns>The innermost matching <see cref="ZoneViewModel"/> from the top of the z-order, or <see langword="null"/>.</returns>
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

    /// <summary>
    /// Updates <see cref="ZoneDropTarget"/> while a box is being dragged over the canvas.
    /// </summary>
    /// <param name="draggedBox">The box being dragged, or <see langword="null"/> to clear the highlight.</param>
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

    /// <summary>
    /// Moves a box by a delta with undo/redo tracking.
    /// </summary>
    /// <param name="box">The box to move.</param>
    /// <param name="deltaX">Horizontal delta in canvas coordinates.</param>
    /// <param name="deltaY">Vertical delta in canvas coordinates.</param>
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

    /// <summary>
    /// Deletes a box and all connections attached to it, with undo/redo tracking.
    /// </summary>
    /// <param name="box">The box to delete.</param>
    private void DeleteBox(StateBoxViewModel box)
    {
        var wasEntry = box.IsEntry;
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
            if (wasEntry)
            {
                var next = Boxes.FirstOrDefault();
                next?.SetIsEntryCore(true);
            }
            _projectService.MarkDirty();
            UpdateTitle();
            NotifyGraphChanged();
        }, () =>
        {
            if (wasEntry)
            {
                foreach (var candidate in Boxes)
                    candidate.SetIsEntryCore(false);
            }

            _projectService.Current.Boxes.Add(box.Model);
            Boxes.Add(box);
            if (wasEntry)
                box.SetIsEntryCore(true);
            foreach (var connection in removedConnections)
            {
                _projectService.Current.Connections.Add(connection.Model);
                Connections.Add(connection);
            }
            _projectService.MarkDirty();
            UpdateTitle();
            NotifyGraphChanged();
        });
    }

    /// <summary>
    /// Removes a step from its parent box and deletes connections attached to that step, with undo/redo tracking.
    /// </summary>
    /// <param name="box">The parent box.</param>
    /// <param name="step">The step to remove.</param>
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

    /// <summary>
    /// Deletes a connection with undo/redo tracking.
    /// </summary>
    /// <param name="connection">The connection to delete.</param>
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

    /// <summary>
    /// Rebuilds all view-model collections from <see cref="IProjectService.Current"/> after load or reset.
    /// </summary>
    private void RefreshFromProject()
    {
        if (IsExecuting)
            _executionCts?.Cancel();

        ClearExecutionHighlight();

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

    /// <summary>
    /// Recomputes <see cref="Title"/> from the project name, dirty flag, and current file path.
    /// </summary>
    private void UpdateTitle()
    {
        var name = _projectService.Current.Name;
        var dirty = _projectService.IsDirty ? " *" : string.Empty;
        var file = _projectService.CurrentFilePath is null
            ? string.Empty
            : $" - {Path.GetFileName(_projectService.CurrentFilePath)}";
        Title = $"{name}{dirty}{file} - VisualStates";
    }

    /// <summary>
    /// Executes an editor mutation through the undo/redo service and refreshes command availability.
    /// </summary>
    /// <param name="description">Human-readable label shown in the undo history.</param>
    /// <param name="execute">The forward action.</param>
    /// <param name="undo">The reverse action.</param>
    private void ExecuteTracked(string description, Action execute, Action undo)
    {
        _undoRedoService.Execute(new ActionCommand(description, execute, undo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Creates a new <see cref="StateStep"/> model with default names and placeholder values for the given kind.
    /// </summary>
    /// <param name="kind">The step kind to instantiate.</param>
    /// <param name="index">One-based index used to disambiguate default step names.</param>
    /// <returns>A new step model ready to add to a box.</returns>
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

/// <summary>
/// Identifies the kind of graph element or step a toolbox entry creates.
/// </summary>
public enum ToolboxItemKind
{
    /// <summary>
    /// Adds a container <see cref="Zone"/> for grouping state boxes.
    /// </summary>
    Zone,

    /// <summary>
    /// Adds a new <see cref="StateBox"/> to the canvas.
    /// </summary>
    StateBox,

    /// <summary>
    /// Adds a <see cref="StepKind.SetVariable"/> step to the selected box.
    /// </summary>
    SetVariable,

    /// <summary>
    /// Adds a <see cref="StepKind.CallEvent"/> step to the selected box.
    /// </summary>
    CallEvent,

    /// <summary>
    /// Adds a <see cref="StepKind.CallMethod"/> step to the selected box.
    /// </summary>
    CallMethod
}

/// <summary>
/// Immutable view-model describing one entry in the editor toolbox.
/// </summary>
public sealed class ToolboxItemViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolboxItemViewModel"/> class.
    /// </summary>
    /// <param name="title">Short label shown in the toolbox UI.</param>
    /// <param name="kind">The action kind invoked when the item is activated.</param>
    /// <param name="description">Longer help text describing what the item adds.</param>
    public ToolboxItemViewModel(string title, ToolboxItemKind kind, string description)
    {
        Title = title;
        Kind = kind;
        Description = description;
    }

    /// <summary>
    /// Gets the short display title of the toolbox item.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the action kind associated with this toolbox item.
    /// </summary>
    public ToolboxItemKind Kind { get; }

    /// <summary>
    /// Gets the descriptive help text for the toolbox item.
    /// </summary>
    public string Description { get; }
}

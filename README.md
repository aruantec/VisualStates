# VisualStates

A visual state machine editor written in C# and Avalonia, inspired by Unreal Engine Blueprints.

## Features

- **Node-based editor** with state boxes and executable steps (Set Variable, Call Event, Call Method)
- **Sequential async execution** driven by connection order
- **C# code generation** producing an async `IGeneratedStateMachine` implementation
- **`.state` project files** (JSON) for saving and loading visual logic
- **Toolbox** for adding boxes and steps
- **Animated connection wires** with flow direction indicators
- **Pan & zoom** navigation (mouse wheel zoom, Shift+drag or middle-mouse pan)
- **Undo / redo**, open, save, delete
- **MVVM** with CommunityToolkit.Mvvm and **dependency injection** via `Microsoft.Extensions.DependencyInjection`

## Projects

| Project | Purpose |
|---------|---------|
| `VisualStates` | Avalonia desktop app (UI, ViewModels, services) |
| `VisualStates.Core` | Domain models, `.state` serialization, code generation, undo/redo |
| `VisualStates.Runtime` | Runtime executor and `IStateMachineContext` contracts |

## Run

```bash
dotnet run --project src/VisualStates/VisualStates.csproj
```

## Usage

1. Use the **Toolbox** to add state boxes and steps.
2. Select a box, then add steps (Set Variable, Call Event, Call Method).
3. Click **Connect from** in the properties panel, then click another box to create an execution connection.
4. Drag boxes to arrange the graph. Use the mouse wheel to zoom; Shift+drag to pan.
5. Use **File → Save** to write a `.state` project file.
6. Use **Generate → Generate C#** to produce async state machine code in the right panel.

## Architecture

### MVVM (CommunityToolkit.Mvvm)

- `MainViewModel`, `StateBoxViewModel`, `StateStepViewModel`, `ConnectionViewModel`
- `[ObservableProperty]` and `[RelayCommand]` for bindings and commands
- `ViewModelBase` extends `ObservableObject`

### Dependency Injection

Services are registered in `ServiceCollectionExtensions` and resolved from `Program.Services`:

| Service | Role |
|---------|------|
| `IProjectService` | Current project, open/save, dirty tracking |
| `IUndoRedoService` | Undo/redo command stack |
| `ICodeGenerationService` | C# code generation |
| `IFileDialogService` | Native file pickers |
| `IWindowContext` | Main window reference for dialogs |
| `MainViewModel` | Root view model (singleton) |
| `MainWindow` | Injected with `MainViewModel` + `IWindowContext` |

### Rendering

- `GraphCanvas` — custom control for grid, state boxes, pins, pan/zoom
- `CompositionConnectionLayer` — animated Bézier wires with flow-direction markers

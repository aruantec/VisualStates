# VisualStates

A visual state machine editor written in C# and Avalonia, inspired by Unreal Engine Blueprints.

## Features

- **Node-based editor** with state boxes and executable steps (Set Variable, Call Event, Call Method)
- **Main entry point** — every new project starts with a **Main** state box marked as the entry point; rename it freely, or right-click any state box and choose **Set as Main Entry Point** to promote another box (only one main at a time)
- **Zones** for grouping states with shared enter/exit and error pins
- **Direction-agnostic pin wiring** — drag from any pin to any pin; the drag start is the source and the drop is the target
- **Debug execute / stop** — step through the happy-path execution order with a 1-second delay and green highlights (`Debug` menu or the play/stop buttons on the top-right); error branches run only when a step actually fails
- **Sequential async execution** driven by connection order (runtime interpreter + generated code)
- **C# code generation** producing an async `IGeneratedStateMachine` implementation
- **`.state` project files** (JSON) for saving and loading visual logic
- **Toolbox** for adding zones, boxes, and steps
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

## Releases

GitHub Actions builds self-contained packages for Windows, macOS, and Linux (no AOT). Push a `v*` tag (for example `v0.1.0`) to create a [GitHub Release](https://github.com/aruantec/VisualStates/releases) with:

- `VisualStates-windows-x64.zip`
- `VisualStates-macos-osx-arm64.zip`
- `VisualStates-macos-osx-Intel-x64.zip`
- `VisualStates-Linux-x86_64.AppImage`
- `VisualStates-aarch64.AppImage`

See [BUILDING.md](BUILDING.md) for local publish and packaging details.

## Usage

1. **File → New** creates a project with a default **Main** state box (already marked as the main entry point). You can rename it; it stays the entry until you assign another box.
2. Use the **Toolbox** to add zones, state boxes, and steps (Set Variable, Call Event, Call Method).
3. Select a box in the properties panel to edit its name, steps, zone membership, or main-entry checkbox.
4. **Right-click a state box** → **Set as Main Entry Point** to make that box the sole graph entry (zones and steps do not get this menu).
5. Drag from a pin on a box, step, or zone to another pin to create a connection. Red error pins define failure branches only.
6. Drag boxes to arrange the graph. Use the mouse wheel to zoom; Shift+drag or middle-mouse to pan.
7. Use **File → Save** to write a `.state` project file.
8. Use **Generate → Generate C#** to produce async state machine code in the right panel.
9. Use **Debug → Execute** (or the green play button) to preview the happy-path order one step at a time; **Stop** (or the stop button) cancels the preview.

## Architecture

### MVVM (CommunityToolkit.Mvvm)

- `MainViewModel`, `StateBoxViewModel`, `StateStepViewModel`, `ConnectionViewModel`, `ZoneViewModel`
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

- `GraphCanvas` — custom control for grid, zones, state boxes, pins, pan/zoom, and box context menus
- `CompositionConnectionLayer` — animated routed wires with flow-direction markers

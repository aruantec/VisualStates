using VisualStates.Core.Models;
using VisualStates.Core.Serialization;

namespace VisualStates.Services;

/// <summary>
/// Default <see cref="IProjectService"/> that keeps the in-memory project,
/// file path, and dirty flag in sync with the disk via
/// <see cref="StateProjectSerializer"/>.
/// </summary>
public sealed class ProjectService : IProjectService
{
    /// <summary>The in-memory project currently being edited.</summary>
    public StateProject Current { get; private set; } = CreateDefaultProject();

    /// <summary>
    /// Absolute path of the last opened/saved <c>.state</c> file, or
    /// <see langword="null"/> for a new project.
    /// </summary>
    public string? CurrentFilePath { get; private set; }

    /// <summary>True when the project has unsaved changes.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>Raised when the current project, path, or dirty flag changes.</summary>
    public event EventHandler? ProjectChanged;

    /// <summary>Replaces the current project with a fresh default document.</summary>
    public void NewProject()
    {
        Current = CreateDefaultProject();
        CurrentFilePath = null;
        IsDirty = false;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Loads a project from <paramref name="path"/> and clears the dirty flag.
    /// </summary>
    /// <param name="path">Path to a <c>.state</c> file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        var project = await StateProjectSerializer.LoadAsync(path, cancellationToken);
        Current = project;
        CurrentFilePath = path;
        IsDirty = false;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Saves the current project to <see cref="CurrentFilePath"/>.
    /// Throws when no path is set — use <see cref="SaveAsAsync"/> first.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentFilePath))
            throw new InvalidOperationException("No file path is set. Use Save As first.");

        await StateProjectSerializer.SaveAsync(Current, CurrentFilePath, cancellationToken);
        IsDirty = false;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Saves the current project to <paramref name="path"/> and updates
    /// <see cref="CurrentFilePath"/>.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveAsAsync(string path, CancellationToken cancellationToken = default)
    {
        CurrentFilePath = path;
        await StateProjectSerializer.SaveAsync(Current, path, cancellationToken);
        IsDirty = false;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Marks the project dirty if it is not already.</summary>
    /// <remarks>
    /// Does not raise <see cref="ProjectChanged"/> — that event reloads the full project
    /// document (New/Open/Save/Replace). Dirty-only updates are handled by callers via
    /// title refresh so the graph view-models are not torn down mid-edit.
    /// </remarks>
    public void MarkDirty()
    {
        if (IsDirty)
            return;

        IsDirty = true;
    }

    /// <summary>
    /// Replaces the in-memory project (e.g. after undo/redo of structural edits)
    /// and clears the dirty flag.
    /// </summary>
    /// <param name="project">New current project.</param>
    /// <param name="filePath">Optional associated file path.</param>
    public void ReplaceProject(StateProject project, string? filePath = null)
    {
        Current = project;
        CurrentFilePath = filePath;
        IsDirty = false;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    private static StateProject CreateDefaultProject() => new()
    {
        Name = "Untitled",
        Boxes =
        [
            new StateBox
            {
                Name = "Main",
                IsEntry = true,
                HeaderColor = "#E74C3C",
                X = 80,
                Y = 80,
                Steps =
                [
                    new StateStep
                    {
                        Name = "Initialize",
                        Kind = StepKind.CallEvent,
                        EventName = "OnEnter"
                    }
                ]
            }
        ]
    };
}

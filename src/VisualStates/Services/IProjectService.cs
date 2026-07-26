using VisualStates.Core.Models;

namespace VisualStates.Services;

/// <summary>
/// Owns the currently loaded <see cref="StateProject"/>, its file path, and dirty state.
/// </summary>
public interface IProjectService
{
    /// <summary>The in-memory project currently being edited.</summary>
    StateProject Current { get; }

    /// <summary>
    /// Absolute path of the last opened/saved <c>.state</c> file, or null for a new project.
    /// </summary>
    string? CurrentFilePath { get; }

    /// <summary>True when the project has unsaved changes.</summary>
    bool IsDirty { get; }

    /// <summary>Raised when the current project, path, or dirty flag changes.</summary>
    event EventHandler? ProjectChanged;

    /// <summary>Replaces the current project with a fresh default document.</summary>
    void NewProject();

    /// <summary>Loads a project from <paramref name="path"/>.</summary>
    /// <param name="path">Path to a <c>.state</c> file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OpenAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the current project to <see cref="CurrentFilePath"/>.
    /// Throws when no path is set — use <see cref="SaveAsAsync"/> first.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the current project to <paramref name="path"/> and updates
    /// <see cref="CurrentFilePath"/>.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Marks the project dirty if it is not already.</summary>
    void MarkDirty();

    /// <summary>
    /// Replaces the in-memory project (e.g. after undo/redo of structural edits)
    /// and clears the dirty flag.
    /// </summary>
    /// <param name="project">New current project.</param>
    /// <param name="filePath">Optional associated file path.</param>
    void ReplaceProject(StateProject project, string? filePath = null);
}

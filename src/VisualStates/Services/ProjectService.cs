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
    /// <inheritdoc />
    public StateProject Current { get; private set; } = CreateDefaultProject();

    /// <inheritdoc />
    public string? CurrentFilePath { get; private set; }

    /// <inheritdoc />
    public bool IsDirty { get; private set; }

    /// <inheritdoc />
    public event EventHandler? ProjectChanged;

    /// <inheritdoc />
    public void NewProject()
    {
        Current = CreateDefaultProject();
        CurrentFilePath = null;
        IsDirty = false;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        var project = await StateProjectSerializer.LoadAsync(path, cancellationToken);
        Current = project;
        CurrentFilePath = path;
        IsDirty = false;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentFilePath))
            throw new InvalidOperationException("No file path is set. Use Save As first.");

        await StateProjectSerializer.SaveAsync(Current, CurrentFilePath, cancellationToken);
        IsDirty = false;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task SaveAsAsync(string path, CancellationToken cancellationToken = default)
    {
        CurrentFilePath = path;
        await StateProjectSerializer.SaveAsync(Current, path, cancellationToken);
        IsDirty = false;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void MarkDirty()
    {
        if (IsDirty)
            return;

        IsDirty = true;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
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
                Name = "Entry",
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

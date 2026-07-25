using VisualStates.Core.Models;
using VisualStates.Core.Serialization;

namespace VisualStates.Services;

public sealed class ProjectService : IProjectService
{
    public StateProject Current { get; private set; } = CreateDefaultProject();
    public string? CurrentFilePath { get; private set; }
    public bool IsDirty { get; private set; }
    public event EventHandler? ProjectChanged;

    public void NewProject()
    {
        Current = CreateDefaultProject();
        CurrentFilePath = null;
        IsDirty = false;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        var project = await StateProjectSerializer.LoadAsync(path, cancellationToken);
        Current = project;
        CurrentFilePath = path;
        IsDirty = false;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentFilePath))
            throw new InvalidOperationException("No file path is set. Use Save As first.");

        await StateProjectSerializer.SaveAsync(Current, CurrentFilePath, cancellationToken);
        IsDirty = false;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveAsAsync(string path, CancellationToken cancellationToken = default)
    {
        CurrentFilePath = path;
        await StateProjectSerializer.SaveAsync(Current, path, cancellationToken);
        IsDirty = false;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkDirty()
    {
        if (IsDirty)
            return;

        IsDirty = true;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

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

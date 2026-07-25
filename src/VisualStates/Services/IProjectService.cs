using VisualStates.Core.Models;

namespace VisualStates.Services;

public interface IProjectService
{
    StateProject Current { get; }
    string? CurrentFilePath { get; }
    bool IsDirty { get; }
    event EventHandler? ProjectChanged;

    void NewProject();
    Task OpenAsync(string path, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task SaveAsAsync(string path, CancellationToken cancellationToken = default);
    void MarkDirty();
    void ReplaceProject(StateProject project, string? filePath = null);
}

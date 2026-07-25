namespace VisualStates.Services;

public interface IFileDialogService
{
    Task<string?> PickOpenFileAsync(string title, string filterName, string extension);
    Task<string?> PickSaveFileAsync(string title, string filterName, string extension, string? defaultFileName = null);
}

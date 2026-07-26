namespace VisualStates.Services;

/// <summary>
/// Platform file-picker abstraction used by the editor for open/save dialogs.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Shows an open-file dialog and returns the selected path, or
    /// <see langword="null"/> when cancelled.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="filterName">Human-readable filter label.</param>
    /// <param name="extension">File extension without the leading dot.</param>
    Task<string?> PickOpenFileAsync(string title, string filterName, string extension);

    /// <summary>
    /// Shows a save-file dialog and returns the chosen path (with extension
    /// appended when missing), or <see langword="null"/> when cancelled.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="filterName">Human-readable filter label.</param>
    /// <param name="extension">File extension without the leading dot.</param>
    /// <param name="defaultFileName">Optional suggested file name.</param>
    Task<string?> PickSaveFileAsync(string title, string filterName, string extension, string? defaultFileName = null);
}

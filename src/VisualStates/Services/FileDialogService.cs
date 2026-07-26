using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace VisualStates.Services;

/// <summary>
/// Avalonia <see cref="IStorageProvider"/>-backed implementation of
/// <see cref="IFileDialogService"/>.
/// </summary>
public sealed class FileDialogService : IFileDialogService
{
    private readonly Func<Window?> _windowProvider;

    /// <summary>
    /// Creates a dialog service that resolves the owner window via
    /// <paramref name="windowProvider"/>.
    /// </summary>
    /// <param name="windowProvider">Lazy provider for the main window.</param>
    public FileDialogService(Func<Window?> windowProvider)
    {
        _windowProvider = windowProvider;
    }

    /// <inheritdoc />
    public async Task<string?> PickOpenFileAsync(string title, string filterName, string extension)
    {
        var window = _windowProvider();
        if (window?.StorageProvider is null)
            return null;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(filterName)
                {
                    Patterns = [$"*.{extension}"]
                }
            ]
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    /// <inheritdoc />
    public async Task<string?> PickSaveFileAsync(string title, string filterName, string extension, string? defaultFileName = null)
    {
        var window = _windowProvider();
        if (window?.StorageProvider is null)
            return null;

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName ?? $"Untitled.{extension}",
            FileTypeChoices =
            [
                new FilePickerFileType(filterName)
                {
                    Patterns = [$"*.{extension}"]
                }
            ]
        });

        var path = file?.Path.LocalPath;
        if (path is not null && !path.EndsWith($".{extension}", StringComparison.OrdinalIgnoreCase))
            path += $".{extension}";

        return path;
    }
}

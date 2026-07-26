using Avalonia.Controls;

namespace VisualStates.Services;

/// <summary>
/// Holds a reference to the application's main window for services that need
/// a dialog owner (e.g. file pickers).
/// </summary>
public interface IWindowContext
{
    /// <summary>The main application window, set once the window is constructed.</summary>
    Window? MainWindow { get; set; }
}

/// <summary>
/// Default <see cref="IWindowContext"/> implementation.
/// </summary>
public sealed class WindowContext : IWindowContext
{
    /// <summary>The main application window, set once the window is constructed.</summary>
    public Window? MainWindow { get; set; }
}

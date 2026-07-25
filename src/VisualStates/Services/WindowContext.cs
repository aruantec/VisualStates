using Avalonia.Controls;

namespace VisualStates.Services;

public interface IWindowContext
{
    Window? MainWindow { get; set; }
}

public sealed class WindowContext : IWindowContext
{
    public Window? MainWindow { get; set; }
}

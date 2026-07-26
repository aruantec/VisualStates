using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace VisualStates;

/// <summary>
/// Avalonia application class: loads XAML resources and assigns the main window from DI.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Loads application XAML and initializes Avalonia resources.
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Resolves <see cref="MainWindow"/> from <see cref="Program.Services"/> for desktop lifetimes.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Program.Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

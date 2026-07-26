using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace VisualStates;

/// <summary>
/// Application entry point: configures dependency injection and starts the Avalonia desktop host.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Root service provider built at startup; used by <see cref="App"/> to resolve the main window.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Configures services, builds the DI container, and launches the Avalonia application.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the desktop lifetime.</param>
    [STAThread]
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddVisualStatesServices();
        Services = services.BuildServiceProvider();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Configures the Avalonia <see cref="App"/> with platform detection, fonts, and logging.
    /// </summary>
    /// <returns>An <see cref="AppBuilder"/> ready to start the desktop lifetime.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}

using Microsoft.Extensions.DependencyInjection;
using VisualStates.Services;
using VisualStates.ViewModels;

namespace VisualStates;

/// <summary>
/// Registers VisualStates editor services and view models with Microsoft.Extensions.DependencyInjection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds singleton and transient services required by the VisualStates desktop application.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddVisualStatesServices(this IServiceCollection services)
    {
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IUndoRedoService, UndoRedoService>();
        services.AddSingleton<ICodeGenerationService, CodeGenerationService>();
        services.AddSingleton<IWindowContext, WindowContext>();
        services.AddSingleton<IFileDialogService>(sp =>
            new FileDialogService(() => sp.GetRequiredService<IWindowContext>().MainWindow));
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();
        return services;
    }
}

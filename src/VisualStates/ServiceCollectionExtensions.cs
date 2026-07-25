using Microsoft.Extensions.DependencyInjection;
using VisualStates.Services;
using VisualStates.ViewModels;

namespace VisualStates;

public static class ServiceCollectionExtensions
{
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

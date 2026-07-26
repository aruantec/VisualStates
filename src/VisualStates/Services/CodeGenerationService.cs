using VisualStates.Core.Generation;
using VisualStates.Core.Models;

namespace VisualStates.Services;

/// <summary>
/// Default <see cref="ICodeGenerationService"/> that delegates to
/// <see cref="StateMachineCodeGenerator"/>.
/// </summary>
public sealed class CodeGenerationService : ICodeGenerationService
{
    private readonly StateMachineCodeGenerator _generator = new();

    /// <summary>
    /// Emits the full generated state-machine class for <paramref name="project"/>.
    /// </summary>
    /// <param name="project">Project to generate from.</param>
    /// <returns>C# source text.</returns>
    public string Generate(StateProject project) => _generator.Generate(project);
}

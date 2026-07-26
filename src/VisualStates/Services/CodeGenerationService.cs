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

    /// <inheritdoc />
    public string Generate(StateProject project) => _generator.Generate(project);
}

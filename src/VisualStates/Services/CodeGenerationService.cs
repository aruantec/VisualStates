using VisualStates.Core.Generation;
using VisualStates.Core.Models;

namespace VisualStates.Services;

public sealed class CodeGenerationService : ICodeGenerationService
{
    private readonly StateMachineCodeGenerator _generator = new();

    public string Generate(StateProject project) => _generator.Generate(project);
}

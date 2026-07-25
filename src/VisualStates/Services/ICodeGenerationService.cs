using VisualStates.Core.Models;

namespace VisualStates.Services;

public interface ICodeGenerationService
{
    string Generate(StateProject project);
}

using VisualStates.Core.Models;

namespace VisualStates.Services;

/// <summary>
/// Generates C# source from a <see cref="StateProject"/>.
/// </summary>
public interface ICodeGenerationService
{
    /// <summary>
    /// Emits the full generated state-machine class for <paramref name="project"/>.
    /// </summary>
    /// <param name="project">Project to generate from.</param>
    /// <returns>C# source text.</returns>
    string Generate(StateProject project);
}

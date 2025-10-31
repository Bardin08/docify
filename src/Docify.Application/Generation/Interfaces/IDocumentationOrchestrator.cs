using Docify.Application.Generation.Models;

namespace Docify.Application.Generation.Interfaces;

/// <summary>
/// Orchestrates the documentation generation workflow
/// </summary>
public interface IDocumentationOrchestrator
{
    /// <summary>
    /// Executes the complete documentation generation workflow
    /// </summary>
    /// <param name="options">Generation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generation result</returns>
    Task<GenerationResult> GenerateAsync(GenerationOptions options, CancellationToken cancellationToken = default);
}

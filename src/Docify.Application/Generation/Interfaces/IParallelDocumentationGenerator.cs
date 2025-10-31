using Docify.Application.Generation.Models;
using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Microsoft.CodeAnalysis;

namespace Docify.Application.Generation.Interfaces;

/// <summary>
/// Generates documentation for multiple APIs in parallel
/// </summary>
public interface IParallelDocumentationGenerator
{
    /// <summary>
    /// Generates documentation for a list of APIs using parallel processing
    /// </summary>
    /// <param name="projectPath">Project path for cache management</param>
    /// <param name="apis">List of APIs to document</param>
    /// <param name="compilation">Roslyn compilation for context collection</param>
    /// <param name="provider">LLM provider to use</param>
    /// <param name="parallelism">Number of concurrent tasks</param>
    /// <param name="dryRun">Whether to cache responses</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of generated documentation</returns>
    Task<List<GeneratedDocumentation>> GenerateAsync(
        string projectPath,
        List<ApiSymbol> apis,
        Compilation compilation,
        ILlmProvider provider,
        int parallelism,
        bool dryRun,
        CancellationToken cancellationToken = default);
}

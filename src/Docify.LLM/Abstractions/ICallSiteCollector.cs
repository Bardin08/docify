using Docify.Core.Models;
using Microsoft.CodeAnalysis;

namespace Docify.LLM.Abstractions;

/// <summary>
/// Collects usage examples (call sites) for an API symbol from within the codebase.
/// </summary>
public interface ICallSiteCollector
{
    /// <summary>
    /// Collects representative usage examples showing how an API is called.
    /// </summary>
    /// <param name="symbol">The API symbol to find usages for.</param>
    /// <param name="compilation">The Roslyn compilation containing the symbol.</param>
    /// <param name="maxExamples">Maximum number of usage examples to collect (default: 5).</param>
    /// <param name="contextLines">Number of lines before/after call site to include for context (default: 3).</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>List of call site information, or empty list if no usages found.</returns>
    Task<List<CallSiteInfo>> CollectCallSites(
        ApiSymbol symbol,
        Compilation compilation,
        int maxExamples = 5,
        int contextLines = 3,
        CancellationToken cancellationToken = default);
}

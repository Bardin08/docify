using Docify.Core.Models;
using Microsoft.CodeAnalysis;

namespace Docify.LLM.Abstractions;

/// <summary>
/// Collects contextual information for an API symbol to enable LLM documentation generation.
/// </summary>
public interface IContextCollector
{
    /// <summary>
    /// Collects detailed context for the specified API symbol.
    /// </summary>
    /// <param name="symbol">The API symbol to analyze.</param>
    /// <param name="compilation">The Roslyn compilation containing the symbol.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The collected API context.</returns>
    Task<ApiContext> CollectContext(
        ApiSymbol symbol,
        Compilation compilation,
        CancellationToken cancellationToken = default);
}

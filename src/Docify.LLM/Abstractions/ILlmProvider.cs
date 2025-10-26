using Docify.Core.Models;
using Docify.LLM.Models;

namespace Docify.LLM.Abstractions;

/// <summary>
/// Abstraction for LLM providers that generate documentation suggestions.
/// Implementations include Anthropic Claude, OpenAI GPT-5, and community-contributed providers.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Generates XML documentation for a given API based on collected context.
    /// </summary>
    /// <param name="context">Contextual information about the API (signature, usage examples, implementation, etc.).</param>
    /// <param name="cancellationToken">Cancellation token to abort the generation request.</param>
    /// <returns>A documentation suggestion including generated XML and metadata.</returns>
    /// <exception cref="InvalidOperationException">Thrown if API key is not configured.</exception>
    /// <exception cref="ProviderException">Thrown if the LLM provider returns an error or times out.</exception>
    Task<DocumentationSuggestion> GenerateDocumentationAsync(
        ApiContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates the cost (in USD) of generating documentation for the given context.
    /// Useful for cost previews before committing to batch operations.
    /// </summary>
    /// <param name="context">Contextual information to estimate token usage and cost.</param>
    /// <returns>Estimated cost in USD.</returns>
    decimal EstimateCost(ApiContext context);

    /// <summary>
    /// Returns the provider name (e.g., "anthropic", "openai").
    /// Used for logging, analytics, and fallback logic.
    /// </summary>
    /// <returns>Provider name in lowercase.</returns>
    string GetProviderName();

    /// <summary>
    /// Checks if the provider is available (API key configured and valid).
    /// Does not make actual API calls; only validates local configuration.
    /// </summary>
    /// <returns>True if the provider has a valid API key configured; otherwise false.</returns>
    Task<bool> IsAvailable();
}

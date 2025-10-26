using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.Models;
using Microsoft.Extensions.Logging;

namespace Docify.LLM.Providers;

/// <summary>
/// Anthropic Claude provider for documentation generation.
/// STUB IMPLEMENTATION - Full implementation in Story 2.5.
/// </summary>
public class ClaudeProvider(ISecretStore secretStore, ILogger<ClaudeProvider> logger) : ILlmProvider
{
    private readonly ISecretStore _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    private readonly ILogger<ClaudeProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<DocumentationSuggestion> GenerateDocumentationAsync(ApiContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Validate API key
        if (!await IsAvailable())
        {
            throw new InvalidOperationException($"API key for {GetProviderName()} not found. Run: docify config set-api-key {GetProviderName()}");
        }

        _logger.LogDebug("ClaudeProvider.GenerateDocumentationAsync called for {ApiSymbolId} (STUB - not yet implemented)", context.ApiSymbolId);

        // TODO: Story 2.5 - Implement actual Claude API integration using Anthropic.SDK
        throw new NotImplementedException("ClaudeProvider implementation pending Story 2.5");
    }

    public decimal EstimateCost(ApiContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Claude Sonnet pricing: $3/M input tokens, $15/M output tokens
        // Estimate: context tokens + 500 output tokens average
        var inputTokens = context.TokenEstimate;
        var outputTokens = 500;

        var inputCost = (inputTokens / 1_000_000m) * 3m;
        var outputCost = (outputTokens / 1_000_000m) * 15m;

        return inputCost + outputCost;
    }

    public string GetProviderName() => "anthropic";

    public async Task<bool> IsAvailable()
    {
        var apiKey = await _secretStore.GetApiKey(GetProviderName());
        return !string.IsNullOrWhiteSpace(apiKey);
    }
}

using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.Models;
using Microsoft.Extensions.Logging;

namespace Docify.LLM.Providers;

/// <summary>
/// OpenAI GPT provider for documentation generation.
/// STUB IMPLEMENTATION - Full implementation in Story 2.6.
/// </summary>
public class GptProvider(ISecretStore secretStore, ILogger<GptProvider> logger) : ILlmProvider
{
    private readonly ISecretStore _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    private readonly ILogger<GptProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<DocumentationSuggestion> GenerateDocumentationAsync(ApiContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Validate API key
        if (!await IsAvailable())
        {
            throw new InvalidOperationException($"API key for {GetProviderName()} not found. Run: docify config set-api-key {GetProviderName()}");
        }

        _logger.LogDebug("GptProvider.GenerateDocumentationAsync called for {ApiSymbolId} (STUB - not yet implemented)", context.ApiSymbolId);

        // TODO: Story 2.6 - Implement actual GPT API integration using OpenAI SDK
        throw new NotImplementedException("GptProvider implementation pending Story 2.6");
    }

    public decimal EstimateCost(ApiContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // GPT-5 pricing TBD - using placeholder estimates
        var inputTokens = context.TokenEstimate;
        var outputTokens = 500;

        var inputCost = (inputTokens / 1_000_000m) * 2m;  // Placeholder
        var outputCost = (outputTokens / 1_000_000m) * 10m;  // Placeholder

        return inputCost + outputCost;
    }

    public string GetProviderName() => "openai";

    public async Task<bool> IsAvailable()
    {
        var apiKey = await _secretStore.GetApiKey(GetProviderName());
        return !string.IsNullOrWhiteSpace(apiKey);
    }
}

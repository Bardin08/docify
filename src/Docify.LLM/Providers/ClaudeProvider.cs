using System.Xml.Linq;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.Exceptions;
using Docify.LLM.Models;
using Docify.LLM.PromptEngineering;
using Docify.LLM.Utilities;
using Docify.LLM.Validation;
using Microsoft.Extensions.Logging;

namespace Docify.LLM.Providers;

/// <summary>
/// Anthropic Claude provider for documentation generation using Anthropic.SDK.
/// Implements retry logic, verbose logging, and prompt engineering best practices.
/// </summary>
public class ClaudeProvider(
    ISecretStore secretStore,
    PromptBuilder promptBuilder,
    OutputValidator outputValidator,
    ILogger<ClaudeProvider> logger) : ILlmProvider
{
    private const string _modelName = "claude-sonnet-4-5";
    private const int _maxTokens = 1000;

    private readonly ISecretStore _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));

    private readonly PromptBuilder _promptBuilder =
        promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));

    private readonly OutputValidator _outputValidator =
        outputValidator ?? throw new ArgumentNullException(nameof(outputValidator));

    private readonly ILogger<ClaudeProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<DocumentationSuggestion> GenerateDocumentationAsync(ApiContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Validate API key
        var apiKey = await _secretStore.GetApiKey(GetProviderName());
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                $"API key for {GetProviderName()} not found. Run: docify config set-api-key {GetProviderName()}");

        // Build prompt
        var promptText = _promptBuilder.BuildPrompt(context);
        _logger.LogDebug("Generated prompt for {ApiSymbolId} ({Length} characters)", context.ApiSymbolId,
            promptText.Length);

        // Create Anthropic client
        var client = new AnthropicClient(new APIAuthentication(apiKey));

        // Prepare messages
        var messages = new List<Message> { new(RoleType.User, promptText) };
        var parameters = new MessageParameters
        {
            Messages = messages,
            MaxTokens = _maxTokens,
            Model = _modelName,
            Stream = false,
            Temperature = 1.0m
        };

        // Execute with retry logic
        MessageResponse response;
        try
        {
            response = await RetryHelper.ExecuteWithRetry(
                async () => await client.Messages.GetClaudeMessageAsync(parameters, cancellationToken),
                maxAttempts: 3,
                initialDelay: TimeSpan.FromSeconds(1),
                backoffMultiplier: 2.0,
                _logger,
                cancellationToken);
        }
        catch (Exception ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            throw new InvalidOperationException(
                $"Invalid API key for {GetProviderName()}. Run: docify config set-api-key {GetProviderName()}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate documentation for {ApiSymbolId} after all retries",
                context.ApiSymbolId);
            throw new ProviderException("anthropic", $"Claude API request failed: {ex.Message}", ex);
        }

        // Extract generated XML
        var textContent = response.Content?.OfType<TextContent>().FirstOrDefault();
        var generatedXml = textContent?.Text ?? string.Empty;

        _logger.LogDebug("Received response from Claude for {ApiSymbolId} ({Length} characters)", context.ApiSymbolId,
            generatedXml.Length);

        // Validate XML using OutputValidator
        var validationResult = _outputValidator.ValidateXmlDocumentation(generatedXml, context);

        if (!validationResult.IsValid)
        {
            foreach (var issue in validationResult.Issues)
                _logger.LogWarning("Validation issue for {ApiSymbolId}: {Issue}", context.ApiSymbolId, issue);

            throw new ProviderException("anthropic", validationResult.Issues[0]);
        }

        // Use cleaned XML if extraction was performed
        if (validationResult.CleanedXml != null)
        {
            _logger.LogInformation("Extracted XML from non-XML response for {ApiSymbolId}", context.ApiSymbolId);
            generatedXml = validationResult.CleanedXml;
        }

        // Log any non-critical issues (warnings)
        if (validationResult.Issues.Count > 0)
        {
            foreach (var issue in validationResult.Issues)
                _logger.LogWarning("Validation warning for {ApiSymbolId}: {Issue}", context.ApiSymbolId, issue);
        }

        // Calculate actual token usage and cost
        var inputTokens = response.Usage?.InputTokens ?? context.TokenEstimate;
        var outputTokens = response.Usage?.OutputTokens ?? 500;
        var totalTokens = inputTokens + outputTokens;

        var inputCost = inputTokens / 1_000_000m * 3m;
        var outputCost = outputTokens / 1_000_000m * 15m;
        var estimatedCost = inputCost + outputCost;

        _logger.LogInformation(
            "Generated documentation for {ApiSymbolId}: {InputTokens} input tokens, {OutputTokens} output tokens, ${Cost:F6} estimated cost",
            context.ApiSymbolId,
            inputTokens,
            outputTokens,
            estimatedCost);

        // Return documentation suggestion
        return new DocumentationSuggestion
        {
            Id = Guid.NewGuid().ToString(),
            ApiSymbolId = context.ApiSymbolId,
            GeneratedXml = generatedXml,
            Provider = GetProviderName(),
            Model = _modelName,
            TokensUsed = totalTokens,
            EstimatedCost = estimatedCost,
            Timestamp = DateTime.UtcNow,
            Status = SuggestionStatus.Pending
        };
    }

    public decimal EstimateCost(ApiContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Claude Sonnet pricing: $3/M input tokens, $15/M output tokens
        // Estimate: context tokens + 500 output tokens average
        var inputTokens = context.TokenEstimate;
        const int outputTokens = 500;

        var inputCost = inputTokens / 1_000_000m * 3m;
        const decimal outputCost = outputTokens / 1_000_000m * 15m;

        return inputCost + outputCost;
    }

    public string GetProviderName() => "anthropic";

    public async Task<bool> IsAvailable()
    {
        var apiKey = await _secretStore.GetApiKey(GetProviderName());
        return !string.IsNullOrWhiteSpace(apiKey);
    }
}

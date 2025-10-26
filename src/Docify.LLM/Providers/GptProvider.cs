using System.Xml.Linq;
using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.Exceptions;
using Docify.LLM.Models;
using Docify.LLM.PromptEngineering;
using Docify.LLM.Utilities;
using Docify.LLM.Validation;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Docify.LLM.Providers;

/// <summary>
/// OpenAI GPT provider for documentation generation using OpenAI SDK.
/// Implements retry logic, verbose logging, and prompt engineering best practices.
/// </summary>
public class GptProvider(
    ISecretStore secretStore,
    PromptBuilder promptBuilder,
    OutputValidator outputValidator,
    ILogger<GptProvider> logger) : ILlmProvider
{
    private const string _modelName = "gpt-5-nano";
    private const int _maxTokens = 1000;
    private const float _temperature = 0.3f;

    private readonly ISecretStore _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));

    private readonly PromptBuilder _promptBuilder =
        promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));

    private readonly OutputValidator _outputValidator =
        outputValidator ?? throw new ArgumentNullException(nameof(outputValidator));

    private readonly ILogger<GptProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<DocumentationSuggestion> GenerateDocumentationAsync(ApiContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var apiKey = await _secretStore.GetApiKey(GetProviderName());
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                $"API key for {GetProviderName()} not found. Run: docify config set-api-key {GetProviderName()}");

        var promptText = _promptBuilder.BuildPrompt(context);
        _logger.LogDebug("Generated prompt for {ApiSymbolId} ({Length} characters)", context.ApiSymbolId,
            promptText.Length);

        var client = new ChatClient(_modelName, apiKey);
        var messages = new List<ChatMessage>
        {
            new UserChatMessage(promptText)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = _maxTokens,
            Temperature = _temperature
        };

        ChatCompletion response;
        try
        {
            response = await RetryHelper.ExecuteWithRetry(
                async () => await client.CompleteChatAsync(messages, options, cancellationToken),
                maxAttempts: 5,
                initialDelay: TimeSpan.FromSeconds(1),
                backoffMultiplier: 2.0,
                _logger,
                cancellationToken);
        }
        catch (Exception ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized") ||
                                     ex.Message.Contains("Incorrect API key"))
        {
            throw new InvalidOperationException(
                $"Invalid API key for {GetProviderName()}. Run: docify config set-api-key {GetProviderName()}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate documentation for {ApiSymbolId} after all retries",
                context.ApiSymbolId);
            throw new ProviderException("openai", $"OpenAI API request failed: {ex.Message}", ex);
        }

        var generatedXml = response.Content[0].Text;

        _logger.LogDebug("Received response from OpenAI for {ApiSymbolId} ({Length} characters)", context.ApiSymbolId,
            generatedXml.Length);

        // Validate XML using OutputValidator
        var validationResult = _outputValidator.ValidateXmlDocumentation(generatedXml, context);

        if (!validationResult.IsValid)
        {
            foreach (var issue in validationResult.Issues)
                _logger.LogWarning("Validation issue for {ApiSymbolId}: {Issue}", context.ApiSymbolId, issue);

            throw new ProviderException("openai", validationResult.Issues[0]);
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
        var inputTokens = response.Usage.InputTokenCount;
        var outputTokens = response.Usage.OutputTokenCount;
        var totalTokens = inputTokens + outputTokens;

        // GPT-5-nano pricing: $0.05/M input tokens, $0.40/M output tokens (as of 2024)
        var inputCost = inputTokens / 1_000_000m * 0.05m;
        var outputCost = outputTokens / 1_000_000m * 0.40m;
        var estimatedCost = inputCost + outputCost;

        _logger.LogInformation(
            "Generated documentation for {ApiSymbolId}: {InputTokens} input tokens, {OutputTokens} output tokens, ${Cost:F6} estimated cost",
            context.ApiSymbolId,
            inputTokens,
            outputTokens,
            estimatedCost);

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

    /// <summary>
    /// Estimates the cost in USD for generating documentation for the given API context.
    /// Uses GPT-5-nano pricing: $0.05 per million input tokens, $0.40 per million output tokens.
    /// </summary>
    /// <param name="context">The API context to estimate cost for.</param>
    /// <returns>Estimated cost in USD.</returns>
    public decimal EstimateCost(ApiContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // GPT-5-nano pricing: $0.05/M input tokens, $0.40/M output tokens
        // Estimate: context tokens + 500 output tokens average
        var inputTokens = context.TokenEstimate;
        const int outputTokens = 500;

        var inputCost = inputTokens / 1_000_000m * 0.05m;
        const decimal outputCost = outputTokens / 1_000_000m * 0.4m;

        return inputCost + outputCost;
    }

    public string GetProviderName() => "openai";

    public async Task<bool> IsAvailable()
    {
        var apiKey = await _secretStore.GetApiKey(GetProviderName());
        return !string.IsNullOrWhiteSpace(apiKey);
    }
}

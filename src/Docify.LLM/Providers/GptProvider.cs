using System.Text.Json;
using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.Exceptions;
using Docify.LLM.Models;
using Docify.LLM.PromptEngineering;
using Docify.LLM.Utilities;
using Docify.LLM.Utils;
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
    private const int _maxTokens = 4000;

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

        // Generate structured output schema
        var fullSchemaJson = OpenAiJsonSchemaUtils.GenerateSchema<XmlDocumentationResponse>(
            "xml_documentation",
            "XML documentation for a C# API");

        // Extract just the "schema" object from the generated JSON
        // The generator produces: { "name": "...", "strict": true, "schema": {...} }
        // But CreateJsonSchemaFormat expects just the inner schema object
        var schemaDocument = JsonDocument.Parse(fullSchemaJson);
        var innerSchema = schemaDocument.RootElement.GetProperty("schema");
        var schemaJson = JsonSerializer.Serialize(innerSchema);

        var schema = BinaryData.FromString(schemaJson);

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = _maxTokens,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "xml_documentation",
                jsonSchema: schema,
                jsonSchemaIsStrict: true)
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

        // Check for refusal first
        if (!string.IsNullOrEmpty(response.Refusal))
        {
            _logger.LogError("OpenAI refused the request: {Refusal}", response.Refusal);
            throw new ProviderException("openai", $"OpenAI refused the request: {response.Refusal}");
        }

        // Check if content exists
        if (response.Content == null || response.Content.Count == 0)
        {
            _logger.LogError("OpenAI returned empty content array");
            throw new ProviderException("openai", "OpenAI returned empty content");
        }

        var contentPart = response.Content.FirstOrDefault();
        if (contentPart == null)
        {
            _logger.LogError("OpenAI returned null content part");
            throw new ProviderException("openai", "OpenAI returned null content part");
        }

        var responseText = contentPart.Text;

        _logger.LogDebug("Raw response from OpenAI for {ApiSymbolId}: {ResponseText}",
            context.ApiSymbolId, responseText ?? "(null)");

        if (string.IsNullOrWhiteSpace(responseText))
        {
            _logger.LogError("OpenAI returned empty or whitespace response text");
            throw new ProviderException("openai", "OpenAI returned empty response text");
        }

        // Parse structured response
        XmlDocumentationResponse? structuredResponse;
        try
        {
            structuredResponse = JsonSerializer.Deserialize<XmlDocumentationResponse>(responseText);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize structured response from OpenAI. Response text: {ResponseText}",
                responseText);
            throw new ProviderException("openai", $"Invalid JSON response from OpenAI: {ex.Message}", ex);
        }

        if (structuredResponse == null || string.IsNullOrWhiteSpace(structuredResponse.XmlDocumentation))
        {
            _logger.LogError("Deserialized response is null or has empty XmlDocumentation");
            throw new ProviderException("openai", "OpenAI returned empty or invalid documentation");
        }

        var generatedXml = structuredResponse.XmlDocumentation;

        _logger.LogDebug("Successfully parsed structured response from OpenAI for {ApiSymbolId} ({Length} characters)",
            context.ApiSymbolId, generatedXml.Length);

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
            foreach (var issue in validationResult.Issues)
                _logger.LogWarning("Validation warning for {ApiSymbolId}: {Issue}", context.ApiSymbolId, issue);

        // Calculate actual token usage and cost
        var inputTokens = response.Usage.InputTokenCount;
        var outputTokens = response.Usage.OutputTokenCount;
        var totalTokens = inputTokens + outputTokens;

        // GPT-4o-mini pricing: $0.15/M input tokens, $0.60/M output tokens (as of 2024)
        var inputCost = inputTokens / 1_000_000m * 0.15m;
        var outputCost = outputTokens / 1_000_000m * 0.60m;
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

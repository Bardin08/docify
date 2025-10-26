namespace Docify.Core.Models;

/// <summary>
/// Configuration for LLM provider settings.
/// Loaded from ~/.docify/config.yaml or environment variables.
/// </summary>
public record LlmConfiguration
{
    /// <summary>
    /// Primary LLM provider name (e.g., "anthropic", "openai").
    /// Must be lowercase.
    /// </summary>
    public required string PrimaryProvider { get; init; }

    /// <summary>
    /// Primary model name (e.g., "claude-sonnet-4-5", "gpt-5-nano").
    /// </summary>
    public required string PrimaryModel { get; init; }

    /// <summary>
    /// Optional fallback provider if primary fails after 5 consecutive attempts.
    /// </summary>
    public string? FallbackProvider { get; init; }

    /// <summary>
    /// Optional fallback model to use with fallback provider.
    /// </summary>
    public string? FallbackModel { get; init; }

    /// <summary>
    /// Validates that provider names are lowercase and non-empty.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PrimaryProvider))
            throw new ArgumentException("PrimaryProvider cannot be null or whitespace.", nameof(PrimaryProvider));

        if (string.IsNullOrWhiteSpace(PrimaryModel))
            throw new ArgumentException("PrimaryModel cannot be null or whitespace.", nameof(PrimaryModel));

        if (!PrimaryProvider.Equals(PrimaryProvider, StringComparison.InvariantCultureIgnoreCase))
            throw new ArgumentException($"PrimaryProvider must be lowercase. Got: '{PrimaryProvider}'",
                nameof(PrimaryProvider));

        if (FallbackProvider != null &&
            !FallbackProvider.Equals(FallbackProvider, StringComparison.InvariantCultureIgnoreCase))
            throw new ArgumentException($"FallbackProvider must be lowercase. Got: '{FallbackProvider}'",
                nameof(FallbackProvider));

        // Validate supported providers
        var supportedProviders = new[] { "anthropic", "openai" };
        if (!supportedProviders.Contains(PrimaryProvider))
            throw new ArgumentException(
                $"Unknown provider '{PrimaryProvider}'. Supported: {string.Join(", ", supportedProviders)}",
                nameof(PrimaryProvider));

        if (FallbackProvider != null && !supportedProviders.Contains(FallbackProvider))
            throw new ArgumentException(
                $"Unknown fallback provider '{FallbackProvider}'. Supported: {string.Join(", ", supportedProviders)}",
                nameof(FallbackProvider));
    }
}

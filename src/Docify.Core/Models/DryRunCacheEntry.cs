namespace Docify.Core.Models;

/// <summary>
/// Represents a single cached LLM response for dry-run mode
/// </summary>
public record DryRunCacheEntry
{
    /// <summary>
    /// Unique identifier of the API symbol
    /// </summary>
    public required string ApiSymbolId { get; init; }

    /// <summary>
    /// Generated XML documentation content
    /// </summary>
    public required string GeneratedXml { get; init; }

    /// <summary>
    /// Timestamp when this response was cached
    /// </summary>
    public required DateTime CachedAt { get; init; }

    /// <summary>
    /// LLM provider used (Claude, GPT-5, etc.)
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Specific model version used
    /// </summary>
    public required string Model { get; init; }
}

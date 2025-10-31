namespace Docify.Core.Models;

/// <summary>
/// Represents the complete dry-run cache for a project
/// </summary>
public record DryRunCache
{
    /// <summary>
    /// SHA256 hash of the project path for cache file naming
    /// </summary>
    public required string ProjectHash { get; init; }

    /// <summary>
    /// List of cached LLM responses
    /// </summary>
    public required List<DryRunCacheEntry> Entries { get; init; }

    /// <summary>
    /// Timestamp when the cache was created
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}

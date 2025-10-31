using Docify.Core.Models;

namespace Docify.Core.Interfaces;

/// <summary>
/// Interface for managing dry-run cache operations
/// Caches LLM responses during dry-run mode to avoid duplicate API calls
/// </summary>
public interface IDryRunCache
{
    /// <summary>
    /// Saves a cache entry for a specific API symbol
    /// </summary>
    /// <param name="projectPath">Path to the project being documented</param>
    /// <param name="entry">Cache entry to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveEntry(string projectPath, DryRunCacheEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the complete cache for a project
    /// </summary>
    /// <param name="projectPath">Path to the project</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The cached data, or null if no cache exists</returns>
    Task<DryRunCache?> LoadCache(string projectPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the cache for a specific project
    /// </summary>
    /// <param name="projectPath">Path to the project</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearCache(string projectPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a cache entry has expired (24 hour threshold)
    /// </summary>
    /// <param name="cachedAt">Timestamp when entry was cached</param>
    /// <returns>True if expired, false otherwise</returns>
    bool IsCacheExpired(DateTime cachedAt);

    /// <summary>
    /// Gets the file path for the cache file
    /// </summary>
    /// <param name="projectPath">Path to the project</param>
    /// <returns>Absolute path to the cache file</returns>
    string GetCacheFilePath(string projectPath);
}

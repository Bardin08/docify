using System.Text.Json;
using Docify.Core.Analyzers;
using Docify.Core.Interfaces;
using Docify.Core.Models;
using Microsoft.Extensions.Logging;

namespace Docify.Core.Caching;

/// <summary>
/// Manages caching of LLM responses during dry-run mode
/// Implements atomic writes and 24-hour expiration for cache entries
/// </summary>
public class DryRunCacheManager : IDryRunCache
{
    private readonly ILogger<DryRunCacheManager> _logger;
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);

    public DryRunCacheManager(ILogger<DryRunCacheManager> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SaveEntry(string projectPath, DryRunCacheEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            var cacheFilePath = GetCacheFilePath(projectPath);
            var cacheDirectory = Path.GetDirectoryName(cacheFilePath)!;
            Directory.CreateDirectory(cacheDirectory);

            var cache = await LoadCache(projectPath, cancellationToken) ??
                        new DryRunCache
                        {
                            ProjectHash = ProjectPathUtils.CalculateProjectHash(projectPath),
                            Entries = [],
                            CreatedAt = DateTime.UtcNow
                        };

            var existingEntry = cache.Entries.FindIndex(e => e.ApiSymbolId == entry.ApiSymbolId);
            if (existingEntry >= 0)
                cache.Entries[existingEntry] = entry;
            else
                cache.Entries.Add(entry);

            var tempFilePath = cacheFilePath + ".tmp";
            var json = JsonSerializer.Serialize(cache, options: new() { WriteIndented = true });

            await File.WriteAllTextAsync(tempFilePath, json, cancellationToken);
            File.Move(tempFilePath, cacheFilePath, overwrite: true);

            _logger.LogDebug("Saved cache entry for API {ApiSymbolId} to {CacheFilePath}",
                entry.ApiSymbolId, cacheFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save cache entry for project {ProjectPath}. Continuing without cache.",
                projectPath);
            // Non-fatal: cache failures should not block the workflow
        }
    }

    /// <inheritdoc />
    public async Task<DryRunCache?> LoadCache(string projectPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectPath);

        try
        {
            var cacheFilePath = GetCacheFilePath(projectPath);
            if (!File.Exists(cacheFilePath))
            {
                _logger.LogDebug("No cache file found at {CacheFilePath}", cacheFilePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(cacheFilePath, cancellationToken);
            var cache = JsonSerializer.Deserialize<DryRunCache>(json);
            if (cache == null)
            {
                _logger.LogWarning("Cache file at {CacheFilePath} is empty or invalid", cacheFilePath);
                return null;
            }

            _logger.LogDebug("Loaded cache with {EntryCount} entries from {CacheFilePath}",
                cache.Entries.Count, cacheFilePath);

            return cache;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse cache file for project {ProjectPath}. Cache will be ignored.",
                projectPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load cache for project {ProjectPath}. Proceeding without cache.",
                projectPath);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task ClearCache(string projectPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectPath);

        try
        {
            var cacheFilePath = GetCacheFilePath(projectPath);
            if (File.Exists(cacheFilePath))
            {
                await Task.Run(() => File.Delete(cacheFilePath), cancellationToken);
                _logger.LogInformation("Cleared cache for project {ProjectPath}", projectPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear cache for project {ProjectPath}", projectPath);
            // Non-fatal
        }
    }

    /// <inheritdoc />
    public bool IsCacheExpired(DateTime cachedAt) => DateTime.UtcNow - cachedAt > _cacheExpiration;

    /// <inheritdoc />
    public string GetCacheFilePath(string projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);

        var projectHash = ProjectPathUtils.CalculateProjectHash(projectPath);
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cacheDirectory = Path.Combine(homeDirectory, ".docify", "cache", projectHash);

        return Path.Combine(cacheDirectory, "dry-run-cache.json");
    }
}

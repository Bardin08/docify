using Docify.Application.Generation.Interfaces;
using Docify.Application.Generation.Models;
using Docify.Core.Interfaces;
using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Docify.Application.Generation.Services;

/// <summary>
/// Generates documentation for multiple APIs in parallel with caching support
/// </summary>
public sealed class ParallelDocumentationGenerator(
    IContextCollector contextCollector,
    IDryRunCache dryRunCache,
    ILlmConfigurationService llmConfigurationService,
    ILogger<ParallelDocumentationGenerator> logger)
    : IParallelDocumentationGenerator
{
    /// <inheritdoc />
    public async Task<List<GeneratedDocumentation>> GenerateAsync(
        string projectPath,
        List<ApiSymbol> apis,
        Compilation compilation,
        ILlmProvider provider,
        int parallelism,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var context = await CreateGenerationContext(projectPath, cancellationToken);
        var config = new GenerationTaskConfig(
            projectPath,
            apis,
            compilation,
            provider,
            parallelism,
            dryRun,
            context);

        var tasks = CreateGenerationTasks(config, cancellationToken);

        await ExecuteGenerationTasks(tasks);
        HandleAuthenticationFailure(context);
        LogCacheStatistics(context);

        return context.Suggestions.ToList();
    }

    private async Task<GenerationContext> CreateGenerationContext(
        string projectPath,
        CancellationToken cancellationToken)
    {
        var cache = await dryRunCache.LoadCache(projectPath, cancellationToken);
        return new GenerationContext { Cache = cache };
    }

    private List<Task> CreateGenerationTasks(
        GenerationTaskConfig config,
        CancellationToken cancellationToken)
    {
        var semaphore = new SemaphoreSlim(config.Parallelism, config.Parallelism);

        return config.Apis.Select(api =>
        {
            var apiConfig = new ApiProcessingConfig(
                config.ProjectPath,
                api,
                config.Compilation,
                config.Provider,
                config.DryRun,
                config.Context,
                semaphore,
                config.Apis.Count);

            return ProcessSingleApi(apiConfig, cancellationToken);
        }).ToList();
    }

    private async Task ProcessSingleApi(
        ApiProcessingConfig config,
        CancellationToken cancellationToken)
    {
        await config.Semaphore.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentIndex = Interlocked.Increment(ref config.Context.CompletedCount);
            var generatedXml = await GetOrGenerateDocumentation(
                config.ProjectPath,
                config.Api,
                config.Compilation,
                config.Provider,
                config.DryRun,
                config.Context,
                currentIndex,
                config.TotalCount,
                cancellationToken);

            AddGeneratedDocumentation(config.Context, config.Api, generatedXml);
        }
        catch (OperationCanceledException)
        {
            // Silently absorb cancellations - they're intentional
        }
        catch (InvalidOperationException ex) when (IsAuthenticationError(ex))
        {
            HandleAuthenticationError(config.Context, ex);
        }
        catch (Exception ex)
        {
            if (config.Context.AuthFailureDetected == 0)
                logger.LogError(ex, "Failed to generate documentation for {ApiName}", config.Api.FullyQualifiedName);
        }
        finally
        {
            config.Semaphore.Release();
        }
    }

    private async Task<string> GetOrGenerateDocumentation(
        string projectPath,
        ApiSymbol api,
        Compilation compilation,
        ILlmProvider provider,
        bool dryRun,
        GenerationContext context,
        int currentIndex,
        int totalCount,
        CancellationToken cancellationToken)
    {
        var cachedEntry = TryGetCachedEntry(context.Cache, api);

        if (cachedEntry != null)
            return HandleCacheHit(context, api, cachedEntry, currentIndex, totalCount);

        return await HandleCacheMiss(
            projectPath,
            api,
            compilation,
            provider,
            dryRun,
            context,
            currentIndex,
            totalCount,
            cancellationToken);
    }

    private DryRunCacheEntry? TryGetCachedEntry(DryRunCache? cache, ApiSymbol api)
    {
        if (cache == null)
            return null;

        return cache.Entries.FirstOrDefault(e =>
            e.ApiSymbolId == api.Id &&
            !dryRunCache.IsCacheExpired(e.CachedAt));
    }

    private string HandleCacheHit(
        GenerationContext context,
        ApiSymbol api,
        DryRunCacheEntry cachedEntry,
        int currentIndex,
        int totalCount)
    {
        Interlocked.Increment(ref context.CacheHits);
        logger.LogInformation("[Progress: {Current}/{Total}] Using cached documentation for {ApiName}",
            currentIndex, totalCount, api.FullyQualifiedName);

        return cachedEntry.GeneratedXml;
    }

    private async Task<string> HandleCacheMiss(
        string projectPath,
        ApiSymbol api,
        Compilation compilation,
        ILlmProvider provider,
        bool dryRun,
        GenerationContext context,
        int currentIndex,
        int totalCount,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref context.CacheMisses);
        logger.LogInformation("[Progress: {Current}/{Total}] Generating documentation for {ApiName}",
            currentIndex, totalCount, api.FullyQualifiedName);

        var apiContext = await contextCollector.CollectContext(api, compilation, cancellationToken);
        var docSuggestion = await provider.GenerateDocumentationAsync(apiContext, cancellationToken);

        if (dryRun)
            await SaveToCacheAsync(projectPath, api, docSuggestion.GeneratedXml, cancellationToken);

        return docSuggestion.GeneratedXml;
    }

    private async Task SaveToCacheAsync(
        string projectPath,
        ApiSymbol api,
        string generatedXml,
        CancellationToken cancellationToken)
    {
        var llmConfig = await llmConfigurationService.LoadConfiguration();

        var cacheEntry = new DryRunCacheEntry
        {
            ApiSymbolId = api.Id,
            GeneratedXml = generatedXml,
            CachedAt = DateTime.UtcNow,
            Provider = llmConfig.PrimaryProvider,
            Model = llmConfig.PrimaryModel
        };

        await dryRunCache.SaveEntry(projectPath, cacheEntry, cancellationToken);
    }

    private static void AddGeneratedDocumentation(GenerationContext context, ApiSymbol api, string generatedXml)
    {
        var doc = new GeneratedDocumentation(api, generatedXml, api.FilePath);
        context.Suggestions.Add(doc);
    }

    private static void HandleAuthenticationError(GenerationContext context, Exception ex)
    {
        if (Interlocked.CompareExchange(ref context.AuthFailureDetected, 1, 0) == 0)
        {
            context.AuthErrorMessage = ex.Message;
        }
    }

    private static bool IsAuthenticationError(Exception ex)
    {
        return ex.Message.Contains("Invalid API key", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("401") ||
               ex.Message.Contains("Unauthorized");
    }

    private static async Task ExecuteGenerationTasks(List<Task> tasks)
    {
        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // All errors are already handled in individual tasks
        }
    }

    private static void HandleAuthenticationFailure(GenerationContext context)
    {
        if (context.AuthFailureDetected == 0)
            return;

        // Core layer should not print to console - that's CLI responsibility
        // Just throw the exception with details, let CLI handle display
        throw new InvalidOperationException($"Authentication failed: {context.AuthErrorMessage}");
    }

    private void LogCacheStatistics(GenerationContext context)
    {
        if (context.Cache != null && (context.CacheHits > 0 || context.CacheMisses > 0))
            logger.LogInformation("Cache statistics: {CacheHits} hits, {CacheMisses} misses",
                context.CacheHits, context.CacheMisses);
    }
}

using Docify.Application.Generation.Interfaces;
using Docify.Application.Generation.Models;
using Docify.Core.Interfaces;
using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.Providers;
using Docify.Writer.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Docify.Application.Generation.Services;

/// <summary>
/// Orchestrates the documentation generation workflow including analysis, generation, and writing
/// </summary>
public sealed class DocumentationOrchestrator(
    ICodeAnalyzer codeAnalyzer,
    LlmProviderFactory llmProviderFactory,
    ILlmConfigurationService llmConfigurationService,
    IParallelDocumentationGenerator parallelGenerator,
    IDocumentationWriter documentationWriter,
    IDryRunCache dryRunCache,
    IPreviewGenerator previewGenerator,
    ILogger<DocumentationOrchestrator> logger)
    : IDocumentationOrchestrator
{
    /// <inheritdoc />
    public async Task<GenerationResult> GenerateAsync(
        GenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting documentation generation for {ProjectPath} (parallelism: {Parallelism})",
            options.ProjectPath, options.Parallelism);

        var (compilation, filteredApis) = await AnalyzeAndFilterApis(options.ProjectPath, options.Intensity);
        if (filteredApis.Count == 0)
        {
            logger.LogInformation("No APIs match the filter criteria. Nothing to generate.");
            return GenerationResult.Error(GenerationStatus.NoApisFound, "No APIs found");
        }

        var provider = await LoadLlmProvider();
        var suggestions = await parallelGenerator.GenerateAsync(
            options.ProjectPath,
            filteredApis,
            compilation,
            provider,
            options.Parallelism,
            options.DryRun,
            cancellationToken);

        if (suggestions.Count == 0)
        {
            logger.LogWarning("No documentation was generated successfully. Nothing to write.");
            return GenerationResult.Error(GenerationStatus.GenerationFailed, "Generation failed");
        }

        logger.LogInformation("Generated {SuccessCount}/{TotalCount} documentation entries",
            suggestions.Count, filteredApis.Count);

        if (options.DryRun)
            return HandleDryRunMode(options.ProjectPath, suggestions);

        return await HandleWriteMode(options.ProjectPath, suggestions, cancellationToken);
    }

    private async Task<(Compilation compilation, List<ApiSymbol> filteredApis)> AnalyzeAndFilterApis(
        string projectPath,
        string intensity)
    {
        var result = await codeAnalyzer.AnalyzeProject(projectPath);
        logger.LogInformation("Analysis complete: {TotalApis} public APIs found", result.PublicApis.Count);

        if (result.HasErrors)
        {
            logger.LogWarning("Compilation has {ErrorCount} diagnostic messages", result.DiagnosticMessages.Count);
            foreach (var diagnostic in result.DiagnosticMessages)
                logger.LogWarning("{Diagnostic}", diagnostic);
        }

        var filteredApis = FilterByIntensity(result.PublicApis, intensity);
        logger.LogInformation("Filtered to {FilteredCount} APIs based on intensity: {Intensity}",
            filteredApis.Count, intensity);

        return (result.Compilation, filteredApis);
    }

    private static List<ApiSymbol> FilterByIntensity(IReadOnlyList<ApiSymbol> apis, string intensity)
    {
        return intensity.ToLowerInvariant() switch
        {
            "undocumented" => apis.Where(a => a.DocumentationStatus == DocumentationStatus.Undocumented).ToList(),
            "partially_documented" => apis
                .Where(a => a.DocumentationStatus == DocumentationStatus.PartiallyDocumented).ToList(),
            "all" => apis.ToList(),
            _ => throw new ArgumentException(
                $"Invalid intensity filter: {intensity}. Valid values: undocumented, partially_documented, all")
        };
    }

    private async Task<ILlmProvider> LoadLlmProvider()
    {
        var llmConfig = await llmConfigurationService.LoadConfiguration();
        return await llmProviderFactory.CreateProvider(llmConfig);
    }

    private GenerationResult HandleDryRunMode(string projectPath, List<GeneratedDocumentation> suggestions)
    {
        var preview = previewGenerator.BuildPreview(suggestions);
        var cacheFilePath = dryRunCache.GetCacheFilePath(projectPath);

        logger.LogInformation("Cached {Count} responses to {CacheFilePath}", suggestions.Count, cacheFilePath);

        return GenerationResult.Success(
            suggestions.Count,
            suggestions.Select(s => s.FilePath).Distinct().Count(),
            preview,
            cacheFilePath);
    }

    private async Task<GenerationResult> HandleWriteMode(
        string projectPath,
        List<GeneratedDocumentation> suggestions,
        CancellationToken cancellationToken)
    {
        var successCount = 0;
        var failedFiles = new List<string>();
        var modifiedFiles = new HashSet<string>();

        for (var i = 0; i < suggestions.Count; i++)
        {
            var suggestion = suggestions[i];
            logger.LogInformation("[Write Progress: {Current}/{Total}] Writing to {FilePath}",
                i + 1, suggestions.Count, suggestion.FilePath);

            try
            {
                var simpleName = suggestion.ApiSymbol.FullyQualifiedName.Split('.').Last();
                var success = await documentationWriter.InsertDocumentation(
                    suggestion.FilePath,
                    projectPath,
                    simpleName,
                    suggestion.XmlDocumentation);

                if (success)
                {
                    successCount++;
                    modifiedFiles.Add(suggestion.FilePath);
                }
                else
                    failedFiles.Add(suggestion.FilePath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write documentation to {FilePath}", suggestion.FilePath);
                failedFiles.Add(suggestion.FilePath);
            }
        }

        // Clear cache after successful write
        await dryRunCache.ClearCache(projectPath, cancellationToken);

        logger.LogInformation("Documentation generation complete: {SuccessCount} successful, {FailedCount} failed",
            successCount, failedFiles.Count);

        var status = failedFiles.Count > 0 ? GenerationStatus.WriteFailed : GenerationStatus.Success;
        var message = failedFiles.Count > 0 ? $"{failedFiles.Count} files failed" : "All files written successfully";

        return new GenerationResult(status, message, 0, successCount, modifiedFiles.Count);
    }
}

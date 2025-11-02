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
    IBackupManager backupManager,
    IDryRunCache dryRunCache,
    IPreviewGenerator previewGenerator,
    IUserConfirmation userConfirmation,
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
        // Group suggestions by file path for efficient batch writing
        var suggestionsByFile = suggestions
            .GroupBy(s => s.FilePath)
            .ToList();

        var totalFiles = suggestionsByFile.Count;
        var totalChanges = suggestions.Count;

        // Prompt user for confirmation
        var confirmed = await userConfirmation.ConfirmBatchWrite(totalChanges, totalFiles).ConfigureAwait(false);
        if (!confirmed)
        {
            logger.LogInformation("User declined to write documentation changes.");
            return GenerationResult.Error(GenerationStatus.WriteFailed, "User cancelled write operation");
        }

        // Create backup before writing
        var filesToBackup = suggestionsByFile.Select(g => g.Key).ToList();
        string? backupPath = null;
        try
        {
            backupPath = await backupManager.CreateBackup(projectPath, filesToBackup).ConfigureAwait(false);
            logger.LogInformation("Created backup at: {BackupPath}", backupPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create backup before write operation");
            return GenerationResult.Error(GenerationStatus.WriteFailed, "Backup creation failed");
        }

        logger.LogInformation("Writing {TotalChanges} documentation changes to {TotalFiles} files...",
            totalChanges, totalFiles);

        var successCount = 0;
        var failedFiles = new List<(string FilePath, string ErrorMessage)>();

        // Batch write loop with file-by-file progress
        for (var i = 0; i < suggestionsByFile.Count; i++)
        {
            var fileGroup = suggestionsByFile[i];
            var filePath = fileGroup.Key;
            var currentFileIndex = i + 1;

            logger.LogInformation("Writing to {FilePath}... [{Current}/{Total}]",
                filePath, currentFileIndex, totalFiles);

            try
            {
                // Write all documentation entries for this file
                foreach (var suggestion in fileGroup)
                {
                    var simpleName = suggestion.ApiSymbol.FullyQualifiedName.Split('.').Last();
                    var success = await documentationWriter.InsertDocumentation(
                        suggestion.FilePath,
                        projectPath,
                        simpleName,
                        suggestion.XmlDocumentation).ConfigureAwait(false);

                    if (!success)
                    {
                        throw new InvalidOperationException($"Documentation insertion failed for {simpleName}");
                    }
                }

                successCount++;
            }
            catch (IOException ex)
            {
                logger.LogWarning("Failed to write to {FilePath}: {ErrorMessage}", filePath, ex.Message);
                failedFiles.Add((filePath, ex.Message));

                // Log rollback suggestion for critical I/O errors
                if (ex.Message.Contains("disk full", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("out of space", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("insufficient", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogError(ex, "Write operation failed due to insufficient disk space.");
                    logger.LogWarning("Consider rollback: docify rollback {BackupPath}", backupPath);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning("Failed to write to {FilePath}: {ErrorMessage}", filePath, ex.Message);
                failedFiles.Add((filePath, ex.Message));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to write to {FilePath}: {ErrorMessage}", filePath, ex.Message);
                failedFiles.Add((filePath, ex.Message));
            }
        }

        // Clear cache after successful write (even with partial success)
        if (successCount > 0)
        {
            await dryRunCache.ClearCache(projectPath, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Dry-run cache cleared.");
        }

        // Display completion summary
        logger.LogInformation("Successfully wrote to {SuccessCount} files.", successCount);
        if (failedFiles.Count > 0)
        {
            logger.LogWarning("{FailedCount} files failed (see log for details).", failedFiles.Count);
            logger.LogWarning("Write operation failed. {SuccessCount} files were modified successfully.", successCount);
            logger.LogInformation("To rollback changes, run: docify rollback {BackupPath}", backupPath);

            foreach (var (filePath, errorMessage) in failedFiles)
                logger.LogWarning("  - {FilePath}: {ErrorMessage}", filePath, errorMessage);
        }

        // TODO: Session state update (POST-MVP - Epic 4)
        // Once Session model exists, update session persistence here:
        // await _sessionManager.UpdateSessionState(sessionId, acceptedSuggestions);
        // await _sessionManager.MarkAsCommitted(sessionId, writtenSymbolIds);

        var status = failedFiles.Count > 0 ? GenerationStatus.WriteFailed : GenerationStatus.Success;
        var message = failedFiles.Count > 0
            ? $"{successCount} files written, {failedFiles.Count} failed"
            : "All files written successfully";

        return new GenerationResult(status, message, 0, totalChanges, successCount);
    }
}

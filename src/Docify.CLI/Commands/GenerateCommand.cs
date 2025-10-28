using System.CommandLine;
using System.Text;
using Docify.Core.Interfaces;
using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.Providers;
using Docify.Writer.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Docify.CLI.Commands;

/// <summary>
/// Command to generate XML documentation for undocumented APIs
/// Implements the batch workflow: Analyze → Generate → Write
/// </summary>
public class GenerateCommand : Command
{
    private readonly ICodeAnalyzer _codeAnalyzer;
    private readonly IContextCollector _contextCollector;
    private readonly LlmProviderFactory _llmProviderFactory;
    private readonly ILlmConfigurationService _llmConfigurationService;
    private readonly IDocumentationWriter _documentationWriter;
    private readonly ILogger<GenerateCommand> _logger;

    public GenerateCommand(
        ICodeAnalyzer codeAnalyzer,
        IContextCollector contextCollector,
        LlmProviderFactory llmProviderFactory,
        ILlmConfigurationService llmConfigurationService,
        IDocumentationWriter documentationWriter,
        ILogger<GenerateCommand> logger)
        : base("generate", "Generate XML documentation for undocumented APIs")
    {
        _codeAnalyzer = codeAnalyzer;
        _contextCollector = contextCollector;
        _llmProviderFactory = llmProviderFactory;
        _llmConfigurationService = llmConfigurationService;
        _documentationWriter = documentationWriter;
        _logger = logger;

        var projectPathArgument = new Argument<string>(
            name: "project-path",
            description: "Path to .csproj or .sln file to analyze");

        var intensityOption = new Option<string>(
            name: "--intensity",
            description: "Filter which APIs to document (undocumented, partially_documented, all)",
            getDefaultValue: () => "undocumented");

        var autoAcceptOption = new Option<bool>(
            name: "--auto-accept",
            description: "Automatically accept all generated documentation without prompts",
            getDefaultValue: () => false);

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Preview changes without writing to files",
            getDefaultValue: () => false);

        var parallelismOption = new Option<int>(
            name: "--parallelism",
            description: "Number of concurrent API requests (1-10, default: 3)",
            getDefaultValue: () => 3);

        AddArgument(projectPathArgument);
        AddOption(intensityOption);
        AddOption(autoAcceptOption);
        AddOption(dryRunOption);
        AddOption(parallelismOption);

        this.SetHandler(async (projectPath, intensity, autoAccept, dryRun, parallelism) =>
        {
            await CommandHandler(projectPath, intensity, autoAccept, dryRun, parallelism);
        }, projectPathArgument, intensityOption, autoAcceptOption, dryRunOption, parallelismOption);
    }

    private async Task CommandHandler(string projectPath, string intensity, bool autoAccept, bool dryRun, int parallelism)
    {
        // Validate parallelism
        if (parallelism is < 1 or > 10)
        {
            _logger.LogError("Parallelism must be between 1 and 10. Got: {Parallelism}", parallelism);
            Environment.ExitCode = 1;
            return;
        }

        using var cts = new CancellationTokenSource();

        try
        {
            _logger.LogInformation("Starting documentation generation for {ProjectPath} (parallelism: {Parallelism})",
                projectPath, parallelism);

            // Stage 1: Analyze and filter
            var (result, filteredApis) = await AnalyzeAndFilterApis(projectPath, intensity);
            if (filteredApis.Count == 0)
            {
                _logger.LogInformation("No APIs match the filter criteria. Nothing to generate.");
                return;
            }

            // Stage 2: Get user confirmation to proceed
            if (!autoAccept && !dryRun && !ConfirmGeneration(filteredApis.Count))
            {
                _logger.LogInformation("Operation cancelled by user");
                return;
            }

            // Stage 3: Load LLM provider
            var provider = await LoadLlmProvider();

            // Stage 4: Generate documentation
            var suggestions = await GenerateDocumentation(filteredApis, result.Compilation, provider, parallelism, cts);
            if (suggestions.Count == 0)
            {
                _logger.LogWarning("No documentation was generated successfully. Nothing to write.");
                return;
            }

            _logger.LogInformation("Generated {SuccessCount}/{TotalCount} documentation entries",
                suggestions.Count, filteredApis.Count);

            // Stage 5: Preview or write
            if (dryRun)
            {
                _logger.LogInformation("Dry-run mode: Previewing changes (not writing to files)");
                PreviewChanges(suggestions);
                return;
            }

            // Stage 6: Confirm write operation
            if (!autoAccept && !ConfirmWrite(suggestions.Count))
            {
                _logger.LogInformation("Write operation cancelled by user");
                return;
            }

            // Stage 7: Write to files
            await WriteDocumentationToFiles(suggestions, projectPath);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Authentication failed"))
        {
            // Authentication error already displayed to user in GenerateDocumentation
            Environment.ExitCode = 1;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Documentation generation was cancelled");
            Environment.ExitCode = 1;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            Environment.ExitCode = 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate documentation: {Message}", ex.Message);
            Environment.ExitCode = 1;
        }
    }

    private async Task<(AnalysisResult result, List<ApiSymbol> filteredApis)> AnalyzeAndFilterApis(
        string projectPath,
        string intensity)
    {
        var result = await _codeAnalyzer.AnalyzeProject(projectPath);
        _logger.LogInformation("Analysis complete: {TotalApis} public APIs found", result.PublicApis.Count);

        if (result.HasErrors)
        {
            _logger.LogWarning("Compilation has {ErrorCount} diagnostic messages", result.DiagnosticMessages.Count);
            foreach (var diagnostic in result.DiagnosticMessages)
                _logger.LogWarning("{Diagnostic}", diagnostic);
        }

        var filteredApis = FilterByIntensity(result.PublicApis, intensity);
        _logger.LogInformation("Filtered to {FilteredCount} APIs based on intensity: {Intensity}",
            filteredApis.Count, intensity);

        return (result, filteredApis);
    }

    private async Task<ILlmProvider> LoadLlmProvider()
    {
        var llmConfig = await _llmConfigurationService.LoadConfiguration();
        return await _llmProviderFactory.CreateProvider(llmConfig);
    }

    private async Task<List<GeneratedDocumentation>> GenerateDocumentation(
        List<ApiSymbol> apis,
        Compilation compilation,
        ILlmProvider provider,
        int parallelism,
        CancellationTokenSource cts)
    {
        var suggestions = new List<GeneratedDocumentation>();
        var completedCount = 0;
        var lockObject = new object();
        var authFailureDetected = false;
        string? authErrorMessage = null;

        var semaphore = new SemaphoreSlim(parallelism, parallelism);
        var tasks = apis.Select(async api =>
        {
            await semaphore.WaitAsync(cts.Token);
            try
            {
                cts.Token.ThrowIfCancellationRequested();

                var currentIndex = Interlocked.Increment(ref completedCount);
                _logger.LogInformation("[Progress: {Current}/{Total}] Generating documentation for {ApiName}",
                    currentIndex, apis.Count, api.FullyQualifiedName);

                var context = await _contextCollector.CollectContext(api, compilation, cts.Token);
                var docSuggestion = await provider.GenerateDocumentationAsync(context, cts.Token);

                var doc = new GeneratedDocumentation
                {
                    ApiSymbol = api,
                    XmlDocumentation = docSuggestion.GeneratedXml,
                    FilePath = api.FilePath
                };

                lock (lockObject)
                {
                    suggestions.Add(doc);
                }
            }
            catch (OperationCanceledException)
            {
                // Silently absorb cancellations - they're intentional
            }
            catch (InvalidOperationException ex) when (IsAuthenticationError(ex))
            {
                lock (lockObject)
                {
                    if (!authFailureDetected)
                    {
                        authFailureDetected = true;
                        authErrorMessage = ex.Message;
                        cts.Cancel();
                    }
                }
            }
            catch (Exception ex)
            {
                if (!authFailureDetected)
                {
                    _logger.LogError(ex, "Failed to generate documentation for {ApiName}", api.FullyQualifiedName);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // All errors are already handled in individual tasks
        }

        if (authFailureDetected)
        {
            Console.WriteLine("\n❌ Authentication Failed");
            Console.WriteLine($"   {authErrorMessage}");
            Console.WriteLine($"   Run: docify config set-api-key <provider>\n");
            throw new InvalidOperationException("Authentication failed");
        }

        return suggestions;
    }

    private bool IsAuthenticationError(Exception ex)
    {
        return ex.Message.Contains("Invalid API key", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("401") ||
               ex.Message.Contains("Unauthorized");
    }

    private async Task WriteDocumentationToFiles(List<GeneratedDocumentation> suggestions, string projectPath)
    {
        var successCount = 0;
        var failedFiles = new List<string>();
        var modifiedFiles = new HashSet<string>();

        for (var i = 0; i < suggestions.Count; i++)
        {
            var suggestion = suggestions[i];
            _logger.LogInformation("[Write Progress: {Current}/{Total}] Writing to {FilePath}",
                i + 1, suggestions.Count, suggestion.FilePath);

            try
            {
                var simpleName = suggestion.ApiSymbol.FullyQualifiedName.Split('.').Last();
                var success = await _documentationWriter.InsertDocumentation(
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
                _logger.LogError(ex, "Failed to write documentation to {FilePath}", suggestion.FilePath);
                failedFiles.Add(suggestion.FilePath);
            }
        }

        DisplayWriteSummary(successCount, modifiedFiles.Count, failedFiles.Count);
    }

    private bool ConfirmGeneration(int apiCount)
    {
        Console.WriteLine($"\n{apiCount} APIs to document");
        Console.Write("Generate documentation? (y/n): ");
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();
        return response is "y" or "yes";
    }

    private bool ConfirmWrite(int suggestionCount)
    {
        Console.WriteLine($"\nGenerated {suggestionCount} documentation entries.");
        Console.Write("Write to files? (y/n): ");
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();
        return response is "y" or "yes";
    }

    private void DisplayWriteSummary(int successCount, int modifiedFilesCount, int failedCount)
    {
        Console.WriteLine($"\n✓ Documentation generation complete!");
        Console.WriteLine($"  {successCount} APIs documented");
        Console.WriteLine($"  {modifiedFilesCount} files modified");

        if (failedCount > 0) Console.WriteLine($"  {failedCount} files failed (see log for details)");

        _logger.LogInformation("Documentation generation complete: {SuccessCount} successful, {FailedCount} failed",
            successCount, failedCount);
    }

    private List<ApiSymbol> FilterByIntensity(IReadOnlyList<ApiSymbol> apis, string intensity)
    {
        return intensity.ToLowerInvariant() switch
        {
            "undocumented" => apis.Where(a => a.DocumentationStatus == DocumentationStatus.Undocumented).ToList(),
            "partially_documented" => apis.Where(a => a.DocumentationStatus == DocumentationStatus.PartiallyDocumented).ToList(),
            "all" => apis.ToList(),
            _ => throw new ArgumentException($"Invalid intensity filter: {intensity}. Valid values: undocumented, partially_documented, all")
        };
    }

    private void PreviewChanges(List<GeneratedDocumentation> suggestions)
    {
        var preview = BuildPreviewOutput(suggestions);
        Console.WriteLine(preview);
    }

    private string BuildPreviewOutput(List<GeneratedDocumentation> suggestions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n=== Dry-Run Preview ===\n");

        var fileGroups = suggestions.GroupBy(s => s.FilePath);

        foreach (var group in fileGroups)
        {
            sb.AppendLine($"File: {group.Key}");
            sb.AppendLine(new string('-', 80));

            foreach (var suggestion in group)
            {
                sb.AppendLine($"\n+ API: {suggestion.ApiSymbol.FullyQualifiedName}");
                sb.AppendLine("+ Documentation:");

                // Format as triple-slash comments
                var lines = suggestion.XmlDocumentation.Split('\n');
                foreach (var line in lines) sb.AppendLine($"+   /// {line.TrimStart()}");
            }

            sb.AppendLine();
        }

        sb.AppendLine($"\nDry-run complete. {suggestions.Count} documentation entries would be added to {fileGroups.Count()} files.");
        sb.AppendLine("Run without --dry-run to apply changes.");

        return sb.ToString();
    }
}

/// <summary>
/// Represents a generated documentation ready to be written to files
/// </summary>
internal class GeneratedDocumentation
{
    public required ApiSymbol ApiSymbol { get; init; }
    public required string XmlDocumentation { get; init; }
    public required string FilePath { get; init; }
}

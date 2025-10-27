using System.CommandLine;
using Docify.Core.Interfaces;
using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.PromptEngineering;
using Docify.LLM.Providers;
using Docify.Writer.Interfaces;
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
    private readonly PromptBuilder _promptBuilder;
    private readonly LlmProviderFactory _llmProviderFactory;
    private readonly ILlmConfigurationService _llmConfigurationService;
    private readonly IDocumentationWriter _documentationWriter;
    private readonly ILogger<GenerateCommand> _logger;

    public GenerateCommand(
        ICodeAnalyzer codeAnalyzer,
        IContextCollector contextCollector,
        PromptBuilder promptBuilder,
        LlmProviderFactory llmProviderFactory,
        ILlmConfigurationService llmConfigurationService,
        IDocumentationWriter documentationWriter,
        ILogger<GenerateCommand> logger)
        : base("generate", "Generate XML documentation for undocumented APIs")
    {
        _codeAnalyzer = codeAnalyzer;
        _contextCollector = contextCollector;
        _promptBuilder = promptBuilder;
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

        AddArgument(projectPathArgument);
        AddOption(intensityOption);
        AddOption(autoAcceptOption);
        AddOption(dryRunOption);

        this.SetHandler(async (projectPath, intensity, autoAccept, dryRun) =>
        {
            await CommandHandler(projectPath, intensity, autoAccept, dryRun);
        }, projectPathArgument, intensityOption, autoAcceptOption, dryRunOption);
    }

    private async Task CommandHandler(string projectPath, string intensity, bool autoAccept, bool dryRun)
    {
        try
        {
            _logger.LogInformation("Starting documentation generation for {ProjectPath}", projectPath);

            // Step 1: Analyze project
            var result = await _codeAnalyzer.AnalyzeProject(projectPath);
            _logger.LogInformation("Analysis complete: {TotalApis} public APIs found", result.PublicApis.Count);

            if (result.HasErrors)
            {
                _logger.LogWarning("Compilation has {ErrorCount} diagnostic messages", result.DiagnosticMessages.Count);
                foreach (var diagnostic in result.DiagnosticMessages)
                {
                    _logger.LogWarning("{Diagnostic}", diagnostic);
                }
            }

            // Step 2: Filter APIs by intensity
            var filteredApis = FilterByIntensity(result.PublicApis, intensity);
            _logger.LogInformation("Filtered to {FilteredCount} APIs based on intensity: {Intensity}",
                filteredApis.Count, intensity);

            if (filteredApis.Count == 0)
            {
                _logger.LogInformation("No APIs match the filter criteria. Nothing to generate.");
                return;
            }

            // Step 3: Display summary and get confirmation
            if (!autoAccept && !dryRun)
            {
                Console.WriteLine($"\n{filteredApis.Count} APIs to document");
                Console.Write("Generate documentation? (y/n): ");
                var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (response != "y" && response != "yes")
                {
                    _logger.LogInformation("Operation cancelled by user");
                    return;
                }
            }

            // Step 4: Load LLM configuration and get provider
            var llmConfig = await _llmConfigurationService.LoadConfiguration();
            var provider = await _llmProviderFactory.CreateProvider(llmConfig);

            // Step 5: Generate documentation for each API
            var suggestions = new List<GeneratedDocumentation>();

            for (int i = 0; i < filteredApis.Count; i++)
            {
                var api = filteredApis[i];
                _logger.LogInformation("[Progress: {Current}/{Total}] Generating documentation for {ApiName}",
                    i + 1, filteredApis.Count, api.FullyQualifiedName);

                try
                {
                    // Collect context
                    var context = await _contextCollector.CollectContext(api, result.Compilation);

                    // Generate documentation
                    var docSuggestion = await provider.GenerateDocumentationAsync(context);

                    suggestions.Add(new GeneratedDocumentation
                    {
                        ApiSymbol = api,
                        XmlDocumentation = docSuggestion.GeneratedXml,
                        FilePath = api.FilePath
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate documentation for {ApiName}", api.FullyQualifiedName);
                    // Continue with other APIs
                }
            }

            _logger.LogInformation("Generated {SuccessCount}/{TotalCount} documentation entries",
                suggestions.Count, filteredApis.Count);

            // Step 5: Dry-run mode - preview only
            if (dryRun)
            {
                _logger.LogInformation("Dry-run mode: Previewing changes (not writing to files)");
                PreviewChanges(suggestions);
                return;
            }

            // Step 6: Confirm write operation
            if (!autoAccept)
            {
                Console.WriteLine($"\nGenerated {suggestions.Count} documentation entries.");
                Console.Write("Write to files? (y/n): ");
                var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (response != "y" && response != "yes")
                {
                    _logger.LogInformation("Write operation cancelled by user");
                    return;
                }
            }

            // Step 7: Write documentation to files
            var successCount = 0;
            var failedFiles = new List<string>();
            var modifiedFiles = new HashSet<string>();

            for (int i = 0; i < suggestions.Count; i++)
            {
                var suggestion = suggestions[i];
                _logger.LogInformation("[Write Progress: {Current}/{Total}] Writing to {FilePath}",
                    i + 1, suggestions.Count, suggestion.FilePath);

                try
                {
                    // Extract simple name from fully qualified name for matching
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
                    {
                        failedFiles.Add(suggestion.FilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to write documentation to {FilePath}", suggestion.FilePath);
                    failedFiles.Add(suggestion.FilePath);
                }
            }

            // Step 8: Display summary
            Console.WriteLine($"\n✓ Documentation generation complete!");
            Console.WriteLine($"  {successCount} APIs documented");
            Console.WriteLine($"  {modifiedFiles.Count} files modified");

            if (failedFiles.Count > 0)
            {
                Console.WriteLine($"  {failedFiles.Count} files failed (see log for details)");
            }

            _logger.LogInformation("Documentation generation complete: {SuccessCount} successful, {FailedCount} failed",
                successCount, failedFiles.Count);
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
        Console.WriteLine("\n=== Dry-Run Preview ===\n");

        var fileGroups = suggestions.GroupBy(s => s.FilePath);

        foreach (var group in fileGroups)
        {
            Console.WriteLine($"File: {group.Key}");
            Console.WriteLine(new string('-', 80));

            foreach (var suggestion in group)
            {
                Console.WriteLine($"\n+ API: {suggestion.ApiSymbol.FullyQualifiedName}");
                Console.WriteLine("+ Documentation:");

                // Format as triple-slash comments
                var lines = suggestion.XmlDocumentation.Split('\n');
                foreach (var line in lines)
                {
                    Console.WriteLine($"+   /// {line.TrimStart()}");
                }
            }

            Console.WriteLine();
        }

        Console.WriteLine($"\nDry-run complete. {suggestions.Count} documentation entries would be added to {fileGroups.Count()} files.");
        Console.WriteLine("Run without --dry-run to apply changes.");
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

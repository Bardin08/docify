using System.CommandLine;
using Docify.CLI.Formatters;
using Docify.Core.Interfaces;
using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Microsoft.Extensions.Logging;

namespace Docify.CLI.Commands;

public class AnalyzeCommand : Command
{
    private readonly ICodeAnalyzer _codeAnalyzer;
    private readonly IReportFormatterFactory _formatterFactory;
    private readonly IContextCollector _contextCollector;
    private readonly ILogger<AnalyzeCommand> _logger;

    public AnalyzeCommand(
        ICodeAnalyzer analyzer,
        IReportFormatterFactory formatterFactory,
        IContextCollector contextCollector,
        ILogger<AnalyzeCommand> logger)
        : base("analyze", "Analyze a .NET project or solution for documentation coverage")
    {
        _codeAnalyzer = analyzer;
        _formatterFactory = formatterFactory;
        _contextCollector = contextCollector;
        _logger = logger;

        var projectPathArgument = new Argument<string>(
            name: "project-path",
            description: "Path to .csproj or .sln file to analyze");

        var formatOption = new Option<string>(
            name: "--format",
            description: "Output format (text, json, markdown)",
            getDefaultValue: () => "text");

        var includeContextOption = new Option<bool>(
            name: "--include-context",
            description: "Include LLM-collected context in the report",
            getDefaultValue: () => false);

        var filePathOption = new Option<string?>(
            name: "--file-path",
            description: "Path to save the report file (if not specified, output to console)");

        AddArgument(projectPathArgument);
        AddOption(formatOption);
        AddOption(includeContextOption);
        AddOption(filePathOption);

        this.SetHandler(async (projectPath, format, includeContext, filePath) =>
        {
            await CommandHandler(projectPath, format, includeContext, filePath);
        }, projectPathArgument, formatOption, includeContextOption, filePathOption);
    }

    private async Task CommandHandler(string projectPath, string format, bool includeContext, string? filePath)
    {
        try
        {
            _logger.LogInformation("Starting analysis of {ProjectPath}", projectPath);
            var result = await _codeAnalyzer.AnalyzeProject(projectPath);

            _logger.LogInformation("Analysis complete: {TotalDocuments} documents found", result.TotalDocuments);

            if (result.HasErrors)
            {
                _logger.LogWarning("Compilation has {ErrorCount} diagnostic messages", result.DiagnosticMessages.Count);
                foreach (var diagnostic in result.DiagnosticMessages)
                {
                    _logger.LogWarning("{Diagnostic}", diagnostic);
                }
            }

            // Collect context for APIs if requested
            if (includeContext)
            {
                _logger.LogInformation("Collecting LLM context for {ApiCount} APIs", result.PublicApis.Count);
                await CollectContextForApis(result);
                _logger.LogInformation("Context collection complete");
            }

            var formatter = _formatterFactory.GetFormatter(format);
            var report = formatter.Format(result, includeContext);

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                // Save report to file
                var directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    _logger.LogInformation("Created directory: {DirectoryPath}", directoryPath);
                }

                await File.WriteAllTextAsync(filePath, report);
                _logger.LogInformation("Report saved to: {FilePath}", filePath);
            }
            else
            {
                // Output to console
                _logger.LogInformation("{Report}", report);
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            Environment.ExitCode = 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze project: {Message}", ex.Message);
            Environment.ExitCode = 1;
        }
    }

    private async Task CollectContextForApis(AnalysisResult result)
    {
        foreach (var api in result.PublicApis)
        {
            try
            {
                var context = await _contextCollector.CollectContext(api, result.Compilation);
                api.Context = context;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect context for {ApiName}", api.FullyQualifiedName);
                // Continue with other APIs even if one fails
            }
        }
    }
}

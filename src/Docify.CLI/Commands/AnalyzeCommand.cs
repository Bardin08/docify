using System.CommandLine;
using Docify.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Docify.CLI.Commands;

public class AnalyzeCommand : Command
{
    private readonly ICodeAnalyzer _codeAnalyzer;
    private readonly ILogger<AnalyzeCommand> _logger;

    public AnalyzeCommand(ICodeAnalyzer analyzer, ILogger<AnalyzeCommand> logger)
        : base("analyze", "Analyze a .NET project or solution for documentation coverage")
    {
        _codeAnalyzer = analyzer;
        _logger = logger;

        var projectPathArgument = new Argument<string>(
            name: "project-path",
            description: "Path to .csproj or .sln file to analyze");

        AddArgument(projectPathArgument);

        this.SetHandler(async projectPath =>
        {
            await CommandHandler(projectPath);
        }, projectPathArgument);
    }

    private async Task CommandHandler(string projectPath)
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze project: {Message}", ex.Message);
            Environment.ExitCode = 1;
        }
    }
}

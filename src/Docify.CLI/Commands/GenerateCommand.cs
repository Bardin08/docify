using System.CommandLine;
using Docify.Application.Generation.Interfaces;
using Docify.Application.Generation.Models;
using Microsoft.Extensions.Logging;

namespace Docify.CLI.Commands;

/// <summary>
/// CLI command for generating XML documentation for undocumented APIs
/// </summary>
public sealed class GenerateCommand : Command
{
    private readonly IDocumentationOrchestrator _orchestrator;
    private readonly ILogger<GenerateCommand> _logger;

    public GenerateCommand(
        IDocumentationOrchestrator orchestrator,
        ILogger<GenerateCommand> logger)
        : base("generate", "Generate XML documentation for undocumented APIs")
    {
        _orchestrator = orchestrator;
        _logger = logger;

        var projectPathArgument = new Argument<string>(
            name: "project-path",
            description: "Path to .csproj or .sln file to analyze");

        var intensityOption = new Option<string>(
            name: "--intensity",
            description: "Filter which APIs to document (undocumented, partially_documented, all)",
            getDefaultValue: () => "undocumented");

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
        AddOption(dryRunOption);
        AddOption(parallelismOption);

        this.SetHandler(async (projectPath, intensity, dryRun, parallelism) =>
        {
            await ExecuteAsync(projectPath, intensity, dryRun, parallelism);
        }, projectPathArgument, intensityOption, dryRunOption, parallelismOption);
    }

    private async Task ExecuteAsync(string projectPath, string intensity, bool dryRun, int parallelism)
    {
        if (!ValidateParallelism(parallelism))
            return;

        using var cts = new CancellationTokenSource();

        try
        {
            var options = new GenerationOptions(projectPath, intensity, parallelism, dryRun);
            var result = await _orchestrator.GenerateAsync(options, cts.Token);
            DisplayResult(result, dryRun);
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }

    private bool ValidateParallelism(int parallelism)
    {
        if (parallelism is >= 1 and <= 10)
            return true;

        _logger.LogError("Parallelism must be between 1 and 10. Got: {Parallelism}", parallelism);
        Environment.ExitCode = 1;
        return false;
    }

    private void DisplayResult(GenerationResult result, bool dryRun)
    {
        switch (result.Status)
        {
            case GenerationStatus.Success:
                if (dryRun)
                {
                    Console.WriteLine(result.PreviewOutput);
                    Console.WriteLine($"\nResponses cached at: {result.CacheFilePath}");
                    Console.WriteLine("Use `docify generate <path>` to apply changes without additional LLM costs.");
                }
                else
                {
                    Console.WriteLine("\n✓ Documentation generation complete!");
                    Console.WriteLine($"  {result.SuccessCount} APIs documented");
                    Console.WriteLine($"  {result.FileCount} files modified");
                }

                break;

            case GenerationStatus.NoApisFound:
                // Already logged by orchestrator
                break;

            case GenerationStatus.GenerationFailed:
            case GenerationStatus.WriteFailed:
                _logger.LogError("Generation failed: {Message}", result.Message);
                Environment.ExitCode = 1;
                break;
        }
    }

    private void HandleError(Exception ex)
    {
        switch (ex)
        {
            case InvalidOperationException when ex.Message.Contains("Authentication failed"):
                DisplayAuthenticationError(ex);
                break;
            case OperationCanceledException:
                _logger.LogWarning("Documentation generation was cancelled");
                break;
            case ArgumentException:
                _logger.LogError("{Message}", ex.Message);
                break;
            default:
                _logger.LogError(ex, "Failed to generate documentation: {Message}", ex.Message);
                break;
        }

        Environment.ExitCode = 1;
    }

    private static void DisplayAuthenticationError(Exception ex)
    {
        Console.WriteLine("\n❌ Authentication Failed");
        Console.WriteLine($"   {ex.Message}");
        Console.WriteLine("   Run: docify config set-api-key <provider>\n");
    }
}

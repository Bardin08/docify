using Docify.Core.Interfaces;
using Docify.Core.Models;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace Docify.Core.Analyzers;

/// <summary>
/// Analyzes .NET projects and solutions using Roslyn.
/// </summary>
public class RoslynAnalyzer(ILogger<RoslynAnalyzer> logger, ISymbolExtractor symbolExtractor) : ICodeAnalyzer
{
    /// <inheritdoc />
    public async Task<AnalysisResult> AnalyzeProject(string projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);

        EnsureMSBuildRegistered();

        var validationResult = ProjectValidator.ValidateProjectPath(projectPath);
        if (!validationResult.IsValid)
            throw new ProjectLoadException(validationResult.ErrorMessage!);

        try
        {
            using var workspace = MSBuildWorkspace.Create();
            workspace.WorkspaceFailed += (_, args) =>
            {
                logger.LogWarning("Workspace diagnostic: {Diagnostic}", args.Diagnostic.Message);
            };

            logger.LogInformation("Loading project from {ProjectPath}", projectPath);

            Compilation? compilation;
            var extension = Path.GetExtension(projectPath);

            if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Solution files are only partially supported. Only the first project will be analyzed.");

                var solution = await workspace.OpenSolutionAsync(projectPath);
                logger.LogInformation("Loaded solution with {ProjectCount} projects", solution.ProjectIds.Count);

                // For multi-project solutions, we'll analyze the first project for now
                // Future stories will handle multi-project scenarios
                var firstProject = solution.Projects.FirstOrDefault()
                                   ?? throw new ProjectLoadException("Solution contains no projects.");

                compilation = await firstProject.GetCompilationAsync();
            }
            else
            {
                var project = await workspace.OpenProjectAsync(projectPath);
                logger.LogInformation("Loaded project {ProjectName}", project.Name);

                compilation = await project.GetCompilationAsync();
            }

            if (compilation == null)
                throw new ProjectLoadException("Failed to create compilation from project.");

            // Extract diagnostics
            var diagnostics = compilation.GetDiagnostics();
            var errorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            var diagnosticMessages = diagnostics
                .Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
                .Select(d => $"{d.Severity}: {d.GetMessage()}")
                .ToList();

            if (errorDiagnostics.Count > 0)
                LogCompilationErrors(errorDiagnostics);

            var syntaxTrees = compilation.SyntaxTrees.ToList();
            var totalDocuments = syntaxTrees.Count;

            // Extract public symbols
            var publicApis = await symbolExtractor.ExtractPublicSymbols(compilation);

            logger.LogInformation("Analysis complete: {TotalDocuments} documents found", totalDocuments);

            return new AnalysisResult
            {
                ProjectPath = projectPath,
                TotalDocuments = totalDocuments,
                Compilation = compilation,
                SyntaxTrees = syntaxTrees,
                HasErrors = errorDiagnostics.Count > 0,
                DiagnosticMessages = diagnosticMessages,
                PublicApis = publicApis
            };
        }
        catch (Exception ex) when (ex is not ProjectLoadException)
        {
            var errorMessage = $"Failed to load project from {projectPath}: {ex.Message}";
            logger.LogError(ex, "{ErrorMessage}", errorMessage);
            throw new ProjectLoadException(errorMessage, ex);
        }
    }

    private void LogCompilationErrors(List<Diagnostic> errorDiagnostics)
    {
        logger.LogWarning("Compilation has {ErrorCount} errors, but continuing with analysis",
            errorDiagnostics.Count);

        var sdkMismatchError = errorDiagnostics.Any(d =>
            d.Id == "CS0518" || d.GetMessage().Contains("SDK", StringComparison.OrdinalIgnoreCase));

        if (sdkMismatchError)
            logger.LogError(
                "Project may require a different .NET SDK version. Run 'dotnet --version' to check installed SDK.");

        var restoreError = errorDiagnostics.Any(d =>
            d.GetMessage().Contains("restore", StringComparison.OrdinalIgnoreCase));

        if (restoreError)
            logger.LogError(
                "Project dependencies may not be restored. Run 'dotnet restore' before analysis.");
    }

    private static void EnsureMSBuildRegistered()
    {
        if (MSBuildLocator.IsRegistered)
            return;

        try
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();

            if (instances.Count > 0)
            {
                var instance = instances.OrderByDescending(i => i.Version).First();
                MSBuildLocator.RegisterInstance(instance);
            }

            // No instances found via QueryVisualStudioInstances
            // This can happen when running via 'dotnet run' or in test scenarios
            // Try RegisterDefaults which uses the currently running SDK
            try
            {
                MSBuildLocator.RegisterDefaults();
            }
            catch (InvalidOperationException)
            {
                // RegisterDefaults failed - MSBuild may already be loaded in the current process
                // This happens when running via dotnet CLI
                // In this case, we can proceed as MSBuild is available, just not registered via MSBuildLocator
                // The workspace will use the already-loaded MSBuild
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new ProjectLoadException(
                "Failed to locate MSBuild. Please ensure .NET SDK is installed. Run 'dotnet --version' to check.",
                ex);
        }
    }
}

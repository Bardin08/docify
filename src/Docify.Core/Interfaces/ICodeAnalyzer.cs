using Docify.Core.Models;

namespace Docify.Core.Interfaces;

/// <summary>
/// Defines the contract for analyzing .NET projects and solutions.
/// </summary>
public interface ICodeAnalyzer
{
    /// <summary>
    /// Analyzes a .NET project or solution file.
    /// </summary>
    /// <param name="projectPath">The path to the .csproj or .sln file.</param>
    /// <returns>The analysis result containing compilation data and diagnostics.</returns>
    /// <exception cref="ProjectLoadException">Thrown when the project cannot be loaded.</exception>
    Task<AnalysisResult> AnalyzeProject(string projectPath);
}

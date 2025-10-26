using Docify.Core.Models;

namespace Docify.CLI.Formatters;

/// <summary>
/// Provides methods for formatting analysis results into different output formats.
/// </summary>
public interface IReportFormatter
{
    /// <summary>
    /// Formats an analysis result into a string representation.
    /// </summary>
    /// <param name="result">The analysis result to format.</param>
    /// <param name="includeContext">Whether to include LLM-collected context in the report.</param>
    /// <returns>A formatted string representation of the analysis result.</returns>
    string Format(AnalysisResult result, bool includeContext = false);
}

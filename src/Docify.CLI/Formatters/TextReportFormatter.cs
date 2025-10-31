using System.Text;
using Docify.Core.Models;
using Microsoft.Extensions.Logging;

namespace Docify.CLI.Formatters;

/// <summary>
/// Formats analysis results as human-readable text output.
/// </summary>
public class TextReportFormatter(ILogger<TextReportFormatter> logger) : ReportFormatterBase, IReportFormatter
{
    /// <inheritdoc/>
    /// <summary>Converts the provided AnalysisResult into a string representation, optionally including contextual information.</summary>
    /// <param name="result">The analysis result to convert into a string.</param>
    /// <param name="includeContext">Indicates whether contextual information is included in the output.</param>
    /// <returns>A string representation of the analysis result, optionally including context.</returns>
    public string Format(AnalysisResult result, bool includeContext = false)
    {
        ArgumentNullException.ThrowIfNull(result);

        logger.LogDebug("Generated text report for {ProjectPath} (includeContext: {IncludeContext})", result.ProjectPath, includeContext);

        var sb = new StringBuilder();
        var projectName = Path.GetFileNameWithoutExtension(result.ProjectPath);
        var summary = result.DocumentationSummary;

        // Header
        sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine($"Documentation Coverage Report: {projectName}");
        sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine();

        // Summary statistics
        if (summary != null)
        {
            sb.AppendLine("Summary:");
            sb.AppendLine($"  Total APIs:        {summary.TotalApis}");
            sb.AppendLine($"  Documented:        {summary.DocumentedCount}");
            sb.AppendLine($"  Undocumented:      {summary.UndocumentedCount}");
            sb.AppendLine($"  Partially Documented: {summary.PartiallyDocumentedCount}");
            sb.AppendLine($"  Coverage:          {summary.CoveragePercentage:F2}%");
            sb.AppendLine();
        }

        // Undocumented APIs
        var undocumentedApis = result.PublicApis
            .Where(api => api.DocumentationStatus == DocumentationStatus.Undocumented)
            .ToList();

        if (undocumentedApis.Count == 0)
        {
            sb.AppendLine("No undocumented APIs found. Great job!");
            return sb.ToString();
        }

        sb.AppendLine($"Undocumented APIs ({undocumentedApis.Count}):");
        sb.AppendLine();

        // Group by namespace
        var byNamespace = undocumentedApis
            .GroupBy(api => GetNamespace(api.FullyQualifiedName))
            .OrderBy(g => g.Key);

        foreach (var nsGroup in byNamespace)
        {
            sb.AppendLine($"Namespace: {nsGroup.Key}");

            // Group by containing type
            var byType = nsGroup
                .GroupBy(api => GetContainingType(api.FullyQualifiedName))
                .OrderBy(g => g.Key);

            foreach (var typeGroup in byType)
            {
                sb.AppendLine($"  Type: {typeGroup.Key}");

                foreach (var api in typeGroup.OrderBy(a => a.LineNumber))
                {
                    var memberName = GetMemberName(api.FullyQualifiedName);
                    sb.AppendLine($"    - {memberName}");
                    sb.AppendLine($"      Signature: {api.Signature}");
                    sb.AppendLine($"      Location:  {api.FilePath}:{api.LineNumber}");

                    if (includeContext && api.Context != null)
                    {
                        sb.AppendLine($"      LLM Context:");
                        sb.AppendLine($"        Implementation Body: {(string.IsNullOrWhiteSpace(api.Context.ImplementationBody) ? "N/A" : $"{api.Context.ImplementationBody.Length} chars")}");
                        sb.AppendLine($"        Called Methods:      {api.Context.CalledMethodsDocumentation.Count}");
                        sb.AppendLine($"        Call Sites:          {api.Context.CallSites.Count}");
                        sb.AppendLine($"        Token Estimate:      {api.Context.TokenEstimate}");
                    }
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}

using System.Text;
using Docify.Core.Models;
using Microsoft.Extensions.Logging;

namespace Docify.CLI.Formatters;

/// <summary>
/// Formats analysis results as Markdown table output.
/// </summary>
public class MarkdownReportFormatter(ILogger<MarkdownReportFormatter> logger) : ReportFormatterBase, IReportFormatter
{
    /// <inheritdoc/>
    public string Format(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        logger.LogDebug("Generated Markdown report for {ProjectPath}", result.ProjectPath);

        var sb = new StringBuilder();
        var projectName = Path.GetFileNameWithoutExtension(result.ProjectPath);
        var summary = result.DocumentationSummary;

        // Summary section
        sb.AppendLine($"# Documentation Coverage Report: {projectName}");
        sb.AppendLine();

        if (summary != null)
        {
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine($"- **Total APIs:** {summary.TotalApis}");
            sb.AppendLine($"- **Documented:** {summary.DocumentedCount}");
            sb.AppendLine($"- **Undocumented:** {summary.UndocumentedCount}");
            sb.AppendLine($"- **Partially Documented:** {summary.PartiallyDocumentedCount}");
            sb.AppendLine($"- **Coverage:** {summary.CoveragePercentage:F2}%");
            sb.AppendLine();
        }

        // Undocumented APIs table
        var undocumentedApis = result.PublicApis
            .Where(api => api.DocumentationStatus == DocumentationStatus.Undocumented)
            .OrderBy(api => GetNamespace(api.FullyQualifiedName))
            .ThenBy(api => GetContainingType(api.FullyQualifiedName))
            .ThenBy(api => GetMemberName(api.FullyQualifiedName))
            .ToList();

        if (undocumentedApis.Count == 0)
        {
            sb.AppendLine("## Undocumented APIs");
            sb.AppendLine();
            sb.AppendLine("No undocumented APIs found. Great job!");
            return sb.ToString();
        }

        sb.AppendLine($"## Undocumented APIs ({undocumentedApis.Count})");
        sb.AppendLine();

        // Table header
        sb.AppendLine("| Namespace | Type | Member | Signature | Location |");
        sb.AppendLine("|-----------|------|--------|-----------|----------|");

        // Table rows
        foreach (var api in undocumentedApis)
        {
            var ns = GetNamespace(api.FullyQualifiedName);
            var type = GetContainingType(api.FullyQualifiedName);
            var member = GetMemberName(api.FullyQualifiedName);
            var signature = api.Signature.Replace("|", "\\|"); // Escape pipe chars
            var location = $"{api.FilePath}:{api.LineNumber}";

            sb.AppendLine($"| {ns} | {type} | {member} | {signature} | {location} |");
        }

        return sb.ToString();
    }
}

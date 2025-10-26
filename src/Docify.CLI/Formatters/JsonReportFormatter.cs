using System.Text.Json;
using Docify.Core.Models;
using Microsoft.Extensions.Logging;

namespace Docify.CLI.Formatters;

/// <summary>
/// Formats analysis results as JSON output.
/// </summary>
public class JsonReportFormatter(ILogger<JsonReportFormatter> logger) : ReportFormatterBase, IReportFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc/>
    public string Format(AnalysisResult result, bool includeContext = false)
    {
        ArgumentNullException.ThrowIfNull(result);

        logger.LogDebug("Generated JSON report for {ProjectPath} (includeContext: {IncludeContext})", result.ProjectPath, includeContext);

        var projectName = Path.GetFileNameWithoutExtension(result.ProjectPath);
        var undocumentedApis = result.PublicApis
            .Where(api => api.DocumentationStatus == DocumentationStatus.Undocumented)
            .Select(api => new UndocumentedApiDto
            {
                FullyQualifiedName = api.FullyQualifiedName,
                Namespace = GetNamespace(api.FullyQualifiedName),
                Signature = api.Signature,
                FilePath = api.FilePath,
                LineNumber = api.LineNumber,
                Context = includeContext ? api.Context : null
            })
            .ToList();

        var reportDto = new ReportDto
        {
            ProjectName = projectName,
            TotalApis = result.DocumentationSummary?.TotalApis ?? 0,
            UndocumentedCount = result.DocumentationSummary?.UndocumentedCount ?? 0,
            CoveragePercentage = result.DocumentationSummary?.CoveragePercentage ?? 0,
            UndocumentedApis = undocumentedApis
        };

        return JsonSerializer.Serialize(reportDto, JsonOptions);
    }
}

/// <summary>
/// DTO for JSON report output.
/// </summary>
public record ReportDto
{
    /// <summary>
    /// Gets the project name.
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// Gets the total number of APIs analyzed.
    /// </summary>
    public required int TotalApis { get; init; }

    /// <summary>
    /// Gets the count of undocumented APIs.
    /// </summary>
    public required int UndocumentedCount { get; init; }

    /// <summary>
    /// Gets the documentation coverage percentage.
    /// </summary>
    public required decimal CoveragePercentage { get; init; }

    /// <summary>
    /// Gets the list of undocumented APIs.
    /// </summary>
    public required List<UndocumentedApiDto> UndocumentedApis { get; init; }
}

/// <summary>
/// DTO for undocumented API information.
/// </summary>
public record UndocumentedApiDto
{
    /// <summary>
    /// Gets the fully qualified name of the API.
    /// </summary>
    public required string FullyQualifiedName { get; init; }

    /// <summary>
    /// Gets the namespace of the API.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// Gets the signature of the API.
    /// </summary>
    public required string Signature { get; init; }

    /// <summary>
    /// Gets the file path where the API is located.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the line number where the API is declared.
    /// </summary>
    public required int LineNumber { get; init; }

    /// <summary>
    /// Gets the LLM context for the API (only included when requested).
    /// </summary>
    public ApiContext? Context { get; init; }
}

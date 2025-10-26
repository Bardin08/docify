namespace Docify.Core.Models;

/// <summary>
/// Summary of documentation coverage for a set of API symbols.
/// </summary>
public record DocumentationSummary
{
    /// <summary>
    /// Total count of public APIs analyzed.
    /// </summary>
    public required int TotalApis { get; init; }

    /// <summary>
    /// Count of APIs with Documented status.
    /// </summary>
    public required int DocumentedCount { get; init; }

    /// <summary>
    /// Count of APIs with Undocumented status.
    /// </summary>
    public required int UndocumentedCount { get; init; }

    /// <summary>
    /// Count of APIs with PartiallyDocumented status.
    /// </summary>
    public required int PartiallyDocumentedCount { get; init; }

    /// <summary>
    /// Documentation coverage percentage (0-100).
    /// </summary>
    public required decimal CoveragePercentage { get; init; }

    /// <summary>
    /// Calculates documentation summary from a list of API symbols.
    /// </summary>
    /// <param name="symbols">The API symbols to analyze.</param>
    /// <returns>A documentation summary with counts and coverage percentage.</returns>
    public static DocumentationSummary Calculate(IReadOnlyList<ApiSymbol> symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);

        var total = symbols.Count;
        var documented = symbols.Count(s => s.DocumentationStatus == DocumentationStatus.Documented);
        var undocumented = symbols.Count(s => s.DocumentationStatus == DocumentationStatus.Undocumented);
        var partiallyDocumented = symbols.Count(s => s.DocumentationStatus == DocumentationStatus.PartiallyDocumented);
        var coverage = total > 0 ? (decimal)documented / total * 100 : 0;

        return new DocumentationSummary
        {
            TotalApis = total,
            DocumentedCount = documented,
            UndocumentedCount = undocumented,
            PartiallyDocumentedCount = partiallyDocumented,
            CoveragePercentage = Math.Round(coverage, 2)
        };
    }
}

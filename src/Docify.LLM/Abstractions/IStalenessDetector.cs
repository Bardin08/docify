using Docify.Core.Models;

namespace Docify.LLM.Abstractions;

/// <summary>
/// Detects whether API documentation is stale based on code changes.
/// </summary>
/// <remarks>
/// STUB INTERFACE: Full implementation will be in Epic 5 (Docify.StateTracker).
/// This stub allows Story 2.3 to proceed without Epic 5 dependencies.
/// </remarks>
public interface IStalenessDetector
{
    /// <summary>
    /// Detects if documentation for the specified API symbol is stale.
    /// </summary>
    /// <param name="symbol">The API symbol to check.</param>
    /// <returns>Result indicating staleness status.</returns>
    StalenessResult DetectStaleDocumentation(ApiSymbol symbol);
}

/// <summary>
/// Result of staleness detection for an API symbol.
/// </summary>
public record StalenessResult
{
    /// <summary>
    /// Indicates if the documentation is stale.
    /// </summary>
    public required bool IsStale { get; init; }

    /// <summary>
    /// Severity of the staleness (if stale).
    /// </summary>
    public StalenessSeverity? Severity { get; init; }

    /// <summary>
    /// Git commit SHA when the API was last modified.
    /// </summary>
    public string? LastModifiedCommit { get; init; }
}

/// <summary>
/// Severity classification for stale documentation.
/// </summary>
public enum StalenessSeverity
{
    /// <summary>
    /// Warning - documentation may be outdated.
    /// </summary>
    Warning,

    /// <summary>
    /// Critical - documentation is definitely outdated.
    /// </summary>
    Critical
}

using Microsoft.CodeAnalysis;

namespace Docify.Core.Models;

/// <summary>
/// Represents the result of analyzing a .NET project or solution.
/// </summary>
public record AnalysisResult
{
    /// <summary>
    /// Gets the path to the analyzed project or solution file.
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// Gets the total number of documents (C# files) found in the analysis.
    /// </summary>
    public required int TotalDocuments { get; init; }

    /// <summary>
    /// Gets the Roslyn compilation object for semantic analysis.
    /// </summary>
    public required Compilation Compilation { get; init; }

    /// <summary>
    /// Gets the list of syntax trees from the compilation.
    /// </summary>
    public required IReadOnlyList<SyntaxTree> SyntaxTrees { get; init; }

    /// <summary>
    /// Gets a value indicating whether the compilation has errors.
    /// </summary>
    public required bool HasErrors { get; init; }

    /// <summary>
    /// Gets the list of diagnostic messages from the compilation.
    /// </summary>
    public required IReadOnlyList<string> DiagnosticMessages { get; init; }

    /// <summary>
    /// Gets the list of discovered public API symbols.
    /// </summary>
    public required IReadOnlyList<ApiSymbol> PublicApis { get; init; }
}

using Docify.Core.Models;
using Microsoft.CodeAnalysis;

namespace Docify.Core.Interfaces;

/// <summary>
/// Provides methods for detecting the documentation status of code symbols.
/// </summary>
public interface IDocumentationDetector
{
    /// <summary>
    /// Detects the documentation status of a symbol by analyzing XML documentation comments.
    /// Checks for presence of summary, param, and returns tags as appropriate.
    /// </summary>
    /// <param name="symbol">The symbol to check for documentation.</param>
    /// <returns>The documentation status (Undocumented, PartiallyDocumented, or Documented).</returns>
    DocumentationStatus DetectDocumentationStatus(ISymbol symbol);

    /// <summary>
    /// Retrieves the raw XML documentation string for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol to get documentation for.</param>
    /// <returns>The XML documentation string, or null if no documentation exists.</returns>
    string? GetXmlDocumentation(ISymbol symbol);
}

using Docify.Core.Models;
using Microsoft.CodeAnalysis;

namespace Docify.Core.Interfaces;

/// <summary>
/// Extracts public API symbols from a Roslyn compilation.
/// </summary>
public interface ISymbolExtractor
{
    /// <summary>
    /// Extracts all public and protected API symbols from the specified compilation.
    /// </summary>
    /// <param name="compilation">The Roslyn compilation to analyze.</param>
    /// <returns>A list of discovered public API symbols.</returns>
    Task<List<ApiSymbol>> ExtractPublicSymbols(Compilation compilation);
}

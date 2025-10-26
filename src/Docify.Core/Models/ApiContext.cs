namespace Docify.Core.Models;

/// <summary>
/// Stores contextual information for an API to enable high-quality LLM documentation generation.
/// </summary>
public record ApiContext
{
    /// <summary>
    /// Reference to the API symbol being documented.
    /// </summary>
    public required string ApiSymbolId { get; init; }

    /// <summary>
    /// Parameter names and types (e.g., ["string name", "int count"]).
    /// </summary>
    public required List<string> ParameterTypes { get; init; }

    /// <summary>
    /// Return type for methods (e.g., "bool") or null for void.
    /// </summary>
    public string? ReturnType { get; init; }

    /// <summary>
    /// Inheritance hierarchy including base classes and implemented interfaces.
    /// </summary>
    public required List<string> InheritanceHierarchy { get; init; }

    /// <summary>
    /// Types referenced in the signature.
    /// </summary>
    public required List<string> RelatedTypes { get; init; }

    /// <summary>
    /// XML documentation from base class or interface methods if available.
    /// </summary>
    public string? XmlDocComments { get; init; }

    /// <summary>
    /// Estimated token count for LLM context.
    /// </summary>
    public required int TokenEstimate { get; init; }

    /// <summary>
    /// Usage examples showing how the API is called in the codebase.
    /// </summary>
    public required List<CallSiteInfo> CallSites { get; init; }

    /// <summary>
    /// Full source code of the method or property implementation body.
    /// </summary>
    public string? ImplementationBody { get; init; }

    /// <summary>
    /// Fresh documentation from internally called methods.
    /// </summary>
    public required List<CalledMethodDoc> CalledMethodsDocumentation { get; init; }

    /// <summary>
    /// Indicates if implementation body was truncated for token budget.
    /// </summary>
    public required bool IsImplementationTruncated { get; init; }
}

/// <summary>
/// Represents a single usage example (call site) of an API within the codebase.
/// </summary>
public record CallSiteInfo
{
    /// <summary>
    /// Absolute path to the file containing the call site.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Line number where the call expression occurs.
    /// </summary>
    public required int LineNumber { get; init; }

    /// <summary>
    /// Lines of code before the call expression for context.
    /// </summary>
    public required List<string> ContextBefore { get; init; }

    /// <summary>
    /// The actual call expression line.
    /// </summary>
    public required string CallExpression { get; init; }

    /// <summary>
    /// Lines of code after the call expression for context.
    /// </summary>
    public required List<string> ContextAfter { get; init; }
}

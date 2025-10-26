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
}

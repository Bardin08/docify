namespace Docify.Core.Models;

/// <summary>
/// Represents a public API symbol discovered during code analysis.
/// </summary>
public record ApiSymbol
{
    /// <summary>
    /// Unique identifier for this symbol.
    /// </summary>
    public required string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The type of symbol (Class, Method, Property, etc.).
    /// </summary>
    public required SymbolType SymbolType { get; init; }

    /// <summary>
    /// Fully qualified name of the symbol (e.g., "MyNamespace.MyClass.MyMethod").
    /// </summary>
    public required string FullyQualifiedName { get; init; }

    /// <summary>
    /// File path where the symbol is declared.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Line number where the symbol is declared (1-indexed).
    /// </summary>
    public required int LineNumber { get; init; }

    /// <summary>
    /// Full signature of the symbol including parameters and return type.
    /// </summary>
    public required string Signature { get; init; }

    /// <summary>
    /// Access modifier of the symbol (e.g., "Public", "Protected").
    /// </summary>
    public required string AccessModifier { get; init; }

    /// <summary>
    /// Indicates whether the symbol is static.
    /// </summary>
    public required bool IsStatic { get; init; }

    /// <summary>
    /// Indicates whether the symbol has any XML documentation.
    /// </summary>
    public required bool HasDocumentation { get; init; }

    /// <summary>
    /// The documentation status of the symbol (Undocumented, PartiallyDocumented, Documented, or Stale).
    /// </summary>
    public required DocumentationStatus DocumentationStatus { get; init; }

    /// <summary>
    /// Contextual information for LLM documentation generation (nullable until collected).
    /// </summary>
    public ApiContext? Context { get; set; }
}

/// <summary>
/// Status of XML documentation for an API symbol.
/// </summary>
public enum DocumentationStatus
{
    /// <summary>
    /// No XML documentation present.
    /// </summary>
    Undocumented,

    /// <summary>
    /// Has summary but missing required param/returns tags.
    /// </summary>
    PartiallyDocumented,

    /// <summary>
    /// Fully documented with all required tags.
    /// </summary>
    Documented,

    /// <summary>
    /// Documentation exists but is out of date (for future staleness detection).
    /// </summary>
    Stale
}

/// <summary>
/// Types of API symbols that can be discovered.
/// </summary>
public enum SymbolType
{
    Class,
    Method,
    Property,
    Interface,
    Event,
    Delegate,
    Struct,
    Enum,
    Indexer
}

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

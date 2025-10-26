namespace Docify.Core.Models;

/// <summary>
/// Represents documentation from a method called within an implementation body.
/// </summary>
public record CalledMethodDoc
{
    /// <summary>
    /// Fully qualified name of the called method.
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// XML documentation of the called method.
    /// </summary>
    public required string XmlDocumentation { get; init; }

    /// <summary>
    /// Indicates if the documentation is fresh (not stale).
    /// </summary>
    public required bool IsFresh { get; init; }
}

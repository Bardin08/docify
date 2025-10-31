using Docify.Core.Models;

namespace Docify.Application.Generation.Models;

/// <summary>
/// Represents a generated documentation ready to be written to files
/// </summary>
public sealed record GeneratedDocumentation(
    ApiSymbol ApiSymbol,
    string XmlDocumentation,
    string FilePath);

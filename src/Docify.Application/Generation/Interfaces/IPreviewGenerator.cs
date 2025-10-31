using Docify.Application.Generation.Models;

namespace Docify.Application.Generation.Interfaces;

/// <summary>
/// Generates preview output for documentation changes
/// </summary>
public interface IPreviewGenerator
{
    /// <summary>
    /// Builds a preview of documentation changes that would be made
    /// </summary>
    /// <param name="suggestions">List of generated documentation to preview</param>
    /// <returns>Formatted preview string</returns>
    string BuildPreview(List<GeneratedDocumentation> suggestions);
}

namespace Docify.Writer.Interfaces;

/// <summary>
/// Inserts XML documentation comments into source files while preserving formatting
/// </summary>
public interface IDocumentationWriter
{
    /// <summary>
    /// Inserts XML documentation for a specific API symbol in a source file
    /// </summary>
    /// <param name="filePath">Absolute path to the source file</param>
    /// <param name="projectPath">Absolute path to the project root (for backup)</param>
    /// <param name="apiIdentifier">Identifier of the API symbol (method name, class name, etc.)</param>
    /// <param name="xmlDocumentation">XML documentation content (without /// prefix)</param>
    /// <returns>True if documentation was successfully inserted, false otherwise</returns>
    Task<bool> InsertDocumentation(string filePath, string projectPath, string apiIdentifier, string xmlDocumentation);
}

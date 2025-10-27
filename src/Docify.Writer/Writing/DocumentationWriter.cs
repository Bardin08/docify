using Docify.Writer.Exceptions;
using Docify.Writer.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Docify.Writer.Writing;

/// <summary>
/// Inserts XML documentation comments into source files using Roslyn
/// </summary>
public class DocumentationWriter : IDocumentationWriter
{
    private readonly IBackupManager _backupManager;
    private readonly ILogger<DocumentationWriter> _logger;

    public DocumentationWriter(IBackupManager backupManager, ILogger<DocumentationWriter> logger)
    {
        ArgumentNullException.ThrowIfNull(backupManager);
        ArgumentNullException.ThrowIfNull(logger);

        _backupManager = backupManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> InsertDocumentation(string filePath, string projectPath, string apiIdentifier, string xmlDocumentation)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(projectPath);
        ArgumentNullException.ThrowIfNull(apiIdentifier);
        ArgumentNullException.ThrowIfNull(xmlDocumentation);

        if (!Path.IsPathFullyQualified(filePath))
            throw new ArgumentException("File path must be absolute", nameof(filePath));

        if (!Path.IsPathFullyQualified(projectPath))
            throw new ArgumentException("Project path must be absolute", nameof(projectPath));

        if (!File.Exists(filePath))
            throw new FileSystemException($"Source file not found: {filePath}");

        try
        {
            // Create backup before modification
            var backupPath = await _backupManager.CreateBackup(projectPath, new[] { filePath }).ConfigureAwait(false);
            _logger.LogInformation("Backup created at: {BackupPath}", backupPath);

            // Load and parse source file
            var sourceContent = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            var lineEnding = FormattingPreserver.DetectLineEnding(sourceContent);

            var sourceText = SourceText.From(sourceContent);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
            var root = await syntaxTree.GetRootAsync().ConfigureAwait(false);

            // Locate API symbol in syntax tree
            var targetNode = FindApiNode(root, apiIdentifier);
            if (targetNode == null)
            {
                throw new AnalysisException($"API symbol '{apiIdentifier}' not found in {filePath}");
            }

            // Extract indentation from the target node
            var indentation = FormattingPreserver.ExtractIndentation(targetNode);

            // Format XML documentation with indentation
            var formattedXml = FormattingPreserver.FormatXmlDocumentation(xmlDocumentation, indentation, lineEnding);

            // Get insertion point (immediately before the API declaration)
            var insertionPosition = targetNode.SpanStart;

            // Insert documentation
            var modifiedContent = sourceContent.Insert(insertionPosition, formattedXml + lineEnding);

            // Write modified content atomically
            await WriteFileAtomically(filePath, modifiedContent).ConfigureAwait(false);

            _logger.LogInformation("Documentation inserted into {FilePath} for API '{ApiIdentifier}'", filePath, apiIdentifier);

            return true;
        }
        catch (AnalysisException)
        {
            // Re-throw analysis exceptions
            throw;
        }
        catch (FileSystemException)
        {
            // Re-throw file system exceptions
            throw;
        }
        catch (Exception ex)
        {
            throw new FileSystemException($"Failed to insert documentation into {filePath}", ex);
        }
    }

    private SyntaxNode? FindApiNode(SyntaxNode root, string apiIdentifier)
    {
        // Try to find method
        var methodNode = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == apiIdentifier);

        if (methodNode != null) return methodNode;

        // Try to find class
        var classNode = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == apiIdentifier);

        if (classNode != null) return classNode;

        // Try to find property
        var propertyNode = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Identifier.Text == apiIdentifier);

        if (propertyNode != null) return propertyNode;

        // Try to find field
        var fieldNode = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.Text == apiIdentifier));

        if (fieldNode != null) return fieldNode;

        // Try to find interface
        var interfaceNode = root.DescendantNodes()
            .OfType<InterfaceDeclarationSyntax>()
            .FirstOrDefault(i => i.Identifier.Text == apiIdentifier);

        if (interfaceNode != null) return interfaceNode;

        // Try to find enum
        var enumNode = root.DescendantNodes()
            .OfType<EnumDeclarationSyntax>()
            .FirstOrDefault(e => e.Identifier.Text == apiIdentifier);

        if (enumNode != null) return enumNode;

        // Try to find struct
        var structNode = root.DescendantNodes()
            .OfType<StructDeclarationSyntax>()
            .FirstOrDefault(s => s.Identifier.Text == apiIdentifier);

        return structNode;
    }

    private async Task WriteFileAtomically(string filePath, string content)
    {
        var tempFilePath = $"{filePath}.tmp";

        try
        {
            await File.WriteAllTextAsync(tempFilePath, content).ConfigureAwait(false);
            File.Move(tempFilePath, filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            // Cleanup temp file if it exists
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            throw new FileSystemException($"Failed to write to file: {filePath}", ex);
        }
    }
}

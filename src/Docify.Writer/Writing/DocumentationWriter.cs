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
    public async Task<bool> InsertDocumentation(string filePath, string projectPath, string apiIdentifier,
        string xmlDocumentation)
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
            var backupPath = await _backupManager.CreateBackup(projectPath, [filePath]);
            _logger.LogInformation("Backup created at: {BackupPath}", backupPath);

            var sourceContent = await File.ReadAllTextAsync(filePath);
            var lineEnding = FormattingPreserver.DetectLineEnding(sourceContent);

            var sourceText = SourceText.From(sourceContent);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
            var root = await syntaxTree.GetRootAsync();

            var targetNode = FindApiNode(root, apiIdentifier);
            if (targetNode == null)
                throw new AnalysisException($"API symbol '{apiIdentifier}' not found in {filePath}");

            var indentation = FormattingPreserver.ExtractIndentation(targetNode);
            var formattedXml = FormattingPreserver.FormatXmlDocumentation(xmlDocumentation, indentation, lineEnding);

            // Get insertion point: We need to insert BEFORE the method's line indentation
            // The leading trivia contains: previous lines + newline + indentation on method's line
            // We want to insert after the newline but before the indentation
            var leadingTriviaFullSpan = targetNode.GetLeadingTrivia().FullSpan;
            var insertionPosition = leadingTriviaFullSpan.End - indentation.Length;

            var modifiedContent = sourceContent.Insert(insertionPosition, formattedXml + lineEnding);

            var syntaxValidationResult = ValidateSyntax(modifiedContent, filePath);
            if (!syntaxValidationResult.IsValid)
            {
                _logger.LogWarning("Syntax errors detected after documentation insertion in {FilePath}: {Errors}",
                    filePath, string.Join(", ", syntaxValidationResult.Errors));

                await _backupManager.RestoreBackup(backupPath, projectPath);

                throw new AnalysisException(
                    $"Documentation insertion caused syntax errors in {filePath}. File restored from backup. Errors: {string.Join(", ", syntaxValidationResult.Errors)}");
            }

            await WriteFileAtomically(filePath, modifiedContent);

            _logger.LogInformation("Documentation inserted into {FilePath} for API '{ApiIdentifier}'", filePath,
                apiIdentifier);

            return true;
        }
        catch (AnalysisException)
        {
            throw;
        }
        catch (FileSystemException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FileSystemException($"Failed to insert documentation into {filePath}", ex);
        }
    }

    private SyntaxNode? FindApiNode(SyntaxNode root, string apiIdentifier)
    {
        var methodNode = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == apiIdentifier);

        if (methodNode != null) return methodNode;

        var classNode = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == apiIdentifier);

        if (classNode != null) return classNode;

        var propertyNode = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Identifier.Text == apiIdentifier);

        if (propertyNode != null) return propertyNode;

        var fieldNode = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.Text == apiIdentifier));

        if (fieldNode != null) return fieldNode;

        var interfaceNode = root.DescendantNodes()
            .OfType<InterfaceDeclarationSyntax>()
            .FirstOrDefault(i => i.Identifier.Text == apiIdentifier);

        if (interfaceNode != null) return interfaceNode;

        var enumNode = root.DescendantNodes()
            .OfType<EnumDeclarationSyntax>()
            .FirstOrDefault(e => e.Identifier.Text == apiIdentifier);

        if (enumNode != null) return enumNode;

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
            await File.WriteAllTextAsync(tempFilePath, content);
            File.Move(tempFilePath, filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            if (!File.Exists(tempFilePath))
                throw new FileSystemException($"Failed to write to file: {filePath}", ex);

            try
            {
                File.Delete(tempFilePath);
            }
            catch
            {
                // Ignore cleanup errors
            }

            throw new FileSystemException($"Failed to write to file: {filePath}", ex);
        }
    }

    /// <summary>
    /// Validates that the modified content has no syntax errors
    /// </summary>
    /// <param name="content">File content to validate</param>
    /// <param name="filePath">File path (for logging)</param>
    /// <returns>Validation result with error details</returns>
    private SyntaxValidationResult ValidateSyntax(string content, string filePath)
    {
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(content);
            var diagnostics = syntaxTree.GetDiagnostics();

            var errors = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage())
                .ToList();

            return errors.Count > 0
                ? new SyntaxValidationResult { IsValid = false, Errors = errors }
                : new SyntaxValidationResult { IsValid = true, Errors = [] };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate syntax for {FilePath}", filePath);
            return new SyntaxValidationResult { IsValid = false, Errors = [$"Syntax validation failed: {ex.Message}"] };
        }
    }

    /// <summary>
    /// Result of syntax validation
    /// </summary>
    private sealed class SyntaxValidationResult
    {
        public required bool IsValid { get; init; }
        public required List<string> Errors { get; init; }
    }
}

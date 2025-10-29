using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Docify.Writer.Writing;

/// <summary>
/// Helper class to preserve formatting when inserting documentation
/// </summary>
public static class FormattingPreserver
{
    /// <summary>
    /// Extracts indentation style from a syntax node
    /// </summary>
    /// <param name="node">Syntax node to analyze</param>
    /// <returns>Indentation string (tabs or spaces)</returns>
    public static string ExtractIndentation(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var leadingTrivia = node.GetLeadingTrivia().ToFullString();

        // Get the last line's indentation (the line the declaration is on)
        var lines = leadingTrivia.Split('\n');
        var lastLine = lines.Length > 0 ? lines[^1] : string.Empty;

        // Extract leading whitespace from the last line
        var indentation = new string(lastLine.TakeWhile(char.IsWhiteSpace).ToArray());

        return indentation;
    }

    /// <summary>
    /// Applies indentation to multi-line XML comments
    /// </summary>
    /// <param name="xmlDocumentation">XML documentation content</param>
    /// <param name="indentation">Indentation string to apply</param>
    /// <param name="lineEnding">Line ending format to use</param>
    /// <returns>Formatted XML documentation with /// prefix and indentation</returns>
    public static string FormatXmlDocumentation(string xmlDocumentation, string indentation, string lineEnding)
    {
        ArgumentNullException.ThrowIfNull(xmlDocumentation);
        ArgumentNullException.ThrowIfNull(indentation);
        ArgumentNullException.ThrowIfNull(lineEnding);

        // First, normalize the XML to have proper line breaks between tags
        var normalizedXml = NormalizeXmlFormatting(xmlDocumentation);

        // Split on both \r\n and \n to handle any input format
        var xmlLines = normalizedXml.Replace("\r\n", "\n").Split('\n');

        var formattedLines = xmlLines
            .Where(line => !string.IsNullOrWhiteSpace(line)) // Remove empty lines
            .Select(line =>
            {
                var trimmedLine = line.Trim();
                return $"{indentation}/// {trimmedLine}";
            });

        return string.Join(lineEnding, formattedLines);
    }

    /// <summary>
    /// Normalizes XML formatting by ensuring tags and content are properly separated
    /// </summary>
    /// <param name="xml">Raw XML content</param>
    /// <returns>Normalized XML with proper line breaks</returns>
    private static string NormalizeXmlFormatting(string xml)
    {
        // Remove any existing leading/trailing whitespace and normalize line endings
        var normalized = xml.Trim().Replace("\r\n", "\n");

        // Strategy:
        // 1. If <tag>\ncontent... -> keep newline after tag (multi-line format)
        // 2. If <tag>content... -> keep on same line (single-line format)

        // If there are already newlines in the content (multi-line)
        if (normalized.Contains('\n'))
        {
            // Clean up excessive whitespace but preserve intentional newlines after opening tags
            // If there's a newline immediately after an opening tag, preserve it
            // Otherwise, remove any accidental spacing

            // Ensure each new opening tag (after a closing tag) is on a new line
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized,
                @"(</[^>]+>)(<[^/])",
                "$1\n$2"
            );

            // Clean up excessive whitespace on lines but don't remove the newlines
            var lines = normalized.Split('\n');
            normalized = string.Join("\n", lines.Select(line => line.Trim()));
        }
        else
        {
            // Single line XML - keep as is, just ensure proper spacing between tags
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized,
                @"(</[^>]+>)(<[^/])",
                "$1\n$2"
            );
        }

        return normalized;
    }

    /// <summary>
    /// Detects the predominant line ending format in a file
    /// </summary>
    /// <param name="content">File content to analyze</param>
    /// <returns>Line ending string ("\r\n" or "\n")</returns>
    public static string DetectLineEnding(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        int crlfCount = content.Split("\r\n").Length - 1;
        int lfOnlyCount = content.Split("\n").Length - 1 - crlfCount;

        return crlfCount > lfOnlyCount ? "\r\n" : "\n";
    }

    /// <summary>
    /// Validates that formatting is preserved after documentation insertion
    /// </summary>
    /// <param name="originalContent">Original file content before modification</param>
    /// <param name="modifiedContent">Modified file content after documentation insertion</param>
    /// <param name="insertedDocumentation">The documentation that was inserted (with line ending)</param>
    /// <returns>True if formatting is preserved, false otherwise</returns>
    public static bool ValidateFormattingPreserved(string originalContent, string modifiedContent, string insertedDocumentation)
    {
        ArgumentNullException.ThrowIfNull(originalContent);
        ArgumentNullException.ThrowIfNull(modifiedContent);
        ArgumentNullException.ThrowIfNull(insertedDocumentation);

        // Normalize line endings for comparison
        var normalizedOriginal = originalContent.Replace("\r\n", "\n");
        var normalizedModified = modifiedContent.Replace("\r\n", "\n");
        var normalizedInserted = insertedDocumentation.Replace("\r\n", "\n");

        // Expected length after insertion
        var expectedLength = normalizedOriginal.Length + normalizedInserted.Length;

        // Quick length check
        if (normalizedModified.Length != expectedLength)
        {
            return false;
        }

        // Remove the inserted documentation from modified content
        var modifiedWithoutDoc = normalizedModified.Replace(normalizedInserted, "", StringComparison.Ordinal);

        // Compare: original should match modified without the documentation
        return string.Equals(normalizedOriginal, modifiedWithoutDoc, StringComparison.Ordinal);
    }

    /// <summary>
    /// Counts blank lines in the leading trivia of a syntax node
    /// </summary>
    /// <param name="node">Syntax node to analyze</param>
    /// <returns>Number of consecutive blank lines before the node</returns>
    public static int CountBlankLinesBefore(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var leadingTrivia = node.GetLeadingTrivia().ToFullString();

        // Split into lines and count consecutive empty lines from the end
        var lines = leadingTrivia.Split('\n');
        int blankLineCount = 0;

        // Count from the end (working backwards from the declaration)
        // Skip the last line as it's the indentation of the current line
        for (int i = lines.Length - 2; i >= 0; i--)
        {
            var line = lines[i].Trim('\r', ' ', '\t');
            if (string.IsNullOrEmpty(line))
            {
                blankLineCount++;
            }
            else
            {
                // Stop when we hit a non-blank line
                break;
            }
        }

        return blankLineCount;
    }

    /// <summary>
    /// Validates that no code outside documentation was modified
    /// </summary>
    /// <param name="originalContent">Original file content</param>
    /// <param name="modifiedContent">Modified file content</param>
    /// <param name="insertedDocumentation">The documentation that was inserted (including line ending)</param>
    /// <param name="insertionPosition">Position where documentation was inserted</param>
    /// <returns>True if only documentation was added, false if other changes detected</returns>
    public static bool ValidateOnlyDocumentationAdded(string originalContent, string modifiedContent, string insertedDocumentation, int insertionPosition)
    {
        ArgumentNullException.ThrowIfNull(originalContent);
        ArgumentNullException.ThrowIfNull(modifiedContent);
        ArgumentNullException.ThrowIfNull(insertedDocumentation);

        if (insertionPosition < 0 || insertionPosition > originalContent.Length)
            return false;

        // Normalize line endings
        var normalizedOriginal = originalContent.Replace("\r\n", "\n");
        var normalizedModified = modifiedContent.Replace("\r\n", "\n");
        var normalizedInserted = insertedDocumentation.Replace("\r\n", "\n");

        // Expected length: original + inserted doc (which includes line ending)
        var expectedLength = normalizedOriginal.Length + normalizedInserted.Length;

        // Check if lengths match
        if (normalizedModified.Length != expectedLength)
        {
            // Length mismatch suggests code was changed beyond documentation
            return false;
        }

        // Verify content before insertion point is unchanged
        var originalBefore = normalizedOriginal.Substring(0, insertionPosition);
        var modifiedBefore = normalizedModified.Substring(0, insertionPosition);

        if (!string.Equals(originalBefore, modifiedBefore, StringComparison.Ordinal))
            return false;

        // Verify content after insertion point is unchanged
        var afterInsertionPosition = insertionPosition + normalizedInserted.Length;

        if (afterInsertionPosition > normalizedModified.Length)
            return false;

        var originalAfter = normalizedOriginal.Substring(insertionPosition);
        var modifiedAfter = normalizedModified.Substring(afterInsertionPosition);

        return string.Equals(originalAfter, modifiedAfter, StringComparison.Ordinal);
    }
}

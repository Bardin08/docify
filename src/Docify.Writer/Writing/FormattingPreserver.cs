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
}

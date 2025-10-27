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

        // Split on both \r\n and \n to handle any input format
        var xmlLines = xmlDocumentation.Replace("\r\n", "\n").Split('\n');

        var formattedLines = xmlLines.Select(line =>
        {
            var trimmedLine = line.TrimStart();
            return trimmedLine.Length > 0
                ? $"{indentation}/// {trimmedLine}"
                : $"{indentation}///";
        });

        return string.Join(lineEnding, formattedLines);
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

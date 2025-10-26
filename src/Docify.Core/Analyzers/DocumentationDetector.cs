using System.Xml.Linq;
using Docify.Core.Interfaces;
using Docify.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Docify.Core.Analyzers;

/// <summary>
/// Detects the documentation status of code symbols by analyzing XML documentation comments.
/// </summary>
public class DocumentationDetector(ILogger<DocumentationDetector> logger) : IDocumentationDetector
{
    /// <inheritdoc/>
    public DocumentationStatus DetectDocumentationStatus(ISymbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        var xmlDoc = GetXmlDocumentation(symbol);

        if (string.IsNullOrWhiteSpace(xmlDoc))
        {
            logger.LogDebug("Checked {SymbolName}: {Status}", symbol.Name, DocumentationStatus.Undocumented);
            return DocumentationStatus.Undocumented;
        }

        try
        {
            var doc = XDocument.Parse(xmlDoc);

            var summaryElement = doc.Descendants("summary").FirstOrDefault();
            if (summaryElement == null || string.IsNullOrWhiteSpace(summaryElement.Value))
            {
                logger.LogDebug("Checked {SymbolName}: {Status} (empty summary)", symbol.Name,
                    DocumentationStatus.Undocumented);
                return DocumentationStatus.Undocumented;
            }

            if (symbol is IMethodSymbol methodSymbol)
                return CheckMethodDocumentation(methodSymbol, doc);

            // For non-method symbols (classes, properties, etc.), summary is sufficient
            logger.LogDebug("Checked {SymbolName}: {Status}", symbol.Name, DocumentationStatus.Documented);
            return DocumentationStatus.Documented;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse XML documentation for {SymbolName}, treating as undocumented",
                symbol.Name);
            return DocumentationStatus.Undocumented;
        }
    }

    /// <inheritdoc/>
    public string? GetXmlDocumentation(ISymbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        return symbol.GetDocumentationCommentXml();
    }

    private DocumentationStatus CheckMethodDocumentation(IMethodSymbol methodSymbol, XDocument doc)
    {
        var missingTags = methodSymbol.Parameters
            .Select(parameter => new
            {
                parameter,
                paramElement = doc.Descendants("param")
                    .FirstOrDefault(p => p.Attribute("name")?.Value == parameter.Name)
            })
            .Where(arg => arg.paramElement == null || string.IsNullOrWhiteSpace(arg.paramElement.Value))
            .Select(arg => $"param:{arg.parameter.Name}")
            .ToList();

        // Check for returns tag (if method returns non-void)
        if (!methodSymbol.ReturnsVoid)
        {
            var returnsElement = doc.Descendants("returns").FirstOrDefault();
            if (returnsElement == null || string.IsNullOrWhiteSpace(returnsElement.Value))
                missingTags.Add("returns");
        }

        if (missingTags.Count > 0)
        {
            logger.LogDebug("Checked {SymbolName}: {Status} (missing: {MissingTags})",
                methodSymbol.Name,
                DocumentationStatus.PartiallyDocumented,
                string.Join(", ", missingTags));
            return DocumentationStatus.PartiallyDocumented;
        }

        logger.LogDebug("Checked {SymbolName}: {Status}", methodSymbol.Name, DocumentationStatus.Documented);
        return DocumentationStatus.Documented;
    }
}

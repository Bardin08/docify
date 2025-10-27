using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Docify.LLM.Models;

/// <summary>
/// Structured response model for XML documentation from OpenAI
/// Ensures we always receive properly formatted documentation
/// </summary>
[Description("XML documentation for a C# API")]
public class XmlDocumentationResponse
{
    /// <summary>
    /// The complete XML documentation block (without /// prefix)
    /// </summary>
    [JsonPropertyName("xmlDocumentation")]
    [Description("Complete XML documentation for the API, including summary, param, returns tags")]
    public required string XmlDocumentation { get; init; }
}

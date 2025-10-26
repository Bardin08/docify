using System.Text;
using Docify.Core.Models;

namespace Docify.LLM.PromptEngineering;

/// <summary>
/// Builds prompts for LLM providers to generate XML documentation from API context.
/// Implements prompt engineering best practices for high-quality documentation generation.
/// </summary>
public class PromptBuilder
{
    /// <summary>
    /// Builds a comprehensive prompt from API context for LLM documentation generation.
    /// </summary>
    /// <param name="context">The API context containing signature, implementation, usage examples, and related documentation.</param>
    /// <returns>A formatted prompt string optimized for LLM documentation generation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    public string BuildPrompt(ApiContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var prompt = new StringBuilder();

        // Task description
        prompt.AppendLine("Generate XML documentation for the following C# API.");
        prompt.AppendLine();

        // API Signature
        prompt.AppendLine("API Signature:");
        prompt.AppendLine($"Symbol ID: {context.ApiSymbolId}");

        if (context.ParameterTypes.Count > 0)
        {
            prompt.AppendLine("Parameters:");
            foreach (var param in context.ParameterTypes) prompt.AppendLine($"  - {param}");
        }

        if (!string.IsNullOrWhiteSpace(context.ReturnType)) prompt.AppendLine($"Return Type: {context.ReturnType}");

        prompt.AppendLine();

        // Implementation Body (EMPHASIZE THIS)
        if (!string.IsNullOrWhiteSpace(context.ImplementationBody))
        {
            prompt.AppendLine("Implementation:");
            prompt.AppendLine("This method/property performs the following:");
            prompt.AppendLine(context.ImplementationBody);

            if (context.IsImplementationTruncated) prompt.AppendLine("(Implementation truncated for token budget)");

            prompt.AppendLine();
        }

        // Called Methods Documentation
        if (context.CalledMethodsDocumentation.Count > 0)
        {
            prompt.AppendLine("Called Methods Documentation:");
            foreach (var calledMethod in context.CalledMethodsDocumentation)
            {
                prompt.AppendLine($"This method calls `{calledMethod.MethodName}` which is documented as:");
                prompt.AppendLine(calledMethod.XmlDocumentation);
                prompt.AppendLine();
            }
        }

        // Type Relationships
        if (context.InheritanceHierarchy.Count > 0)
        {
            prompt.AppendLine("Type Relationships:");
            prompt.AppendLine($"Inheritance hierarchy: {string.Join(" -> ", context.InheritanceHierarchy)}");
            prompt.AppendLine();
        }

        if (context.RelatedTypes.Count > 0 && context.RelatedTypes.Count <= 10)
        {
            prompt.AppendLine($"Related types: {string.Join(", ", context.RelatedTypes)}");
            prompt.AppendLine();
        }

        // Usage Examples
        if (context.CallSites.Count > 0)
        {
            prompt.AppendLine("Usage Examples:");
            var exampleCount = Math.Min(context.CallSites.Count, 3);
            for (int i = 0; i < exampleCount; i++)
            {
                var callSite = context.CallSites[i];
                prompt.AppendLine($"Example {i + 1} ({callSite.FilePath}:{callSite.LineNumber}):");

                if (callSite.ContextBefore.Count > 0)
                    foreach (var line in callSite.ContextBefore)
                        prompt.AppendLine($"  {line}");

                prompt.AppendLine($"  {callSite.CallExpression}");

                if (callSite.ContextAfter.Count > 0)
                    foreach (var line in callSite.ContextAfter)
                        prompt.AppendLine($"  {line}");

                prompt.AppendLine();
            }
        }

        // Related Documentation
        if (!string.IsNullOrWhiteSpace(context.XmlDocComments))
        {
            prompt.AppendLine("Related Documentation:");
            prompt.AppendLine("Base class or interface documentation:");
            prompt.AppendLine(context.XmlDocComments);
            prompt.AppendLine();
        }

        // Output Format Specification
        prompt.AppendLine("Output Format:");
        prompt.AppendLine("- Generate valid XML documentation with appropriate tags");
        prompt.AppendLine("- Use <summary> tag (required) - 1-2 concise sentences describing WHAT the method does");
        prompt.AppendLine("- Use <param> tags for each parameter - describe the PURPOSE of each parameter");
        prompt.AppendLine("- Use <returns> tag if method returns a value - describe WHAT is returned");
        prompt.AppendLine("- Use <remarks> tag ONLY if there are important caveats or complex behavior to explain");
        prompt.AppendLine("- Use <exception> tags if the implementation explicitly throws exceptions");
        prompt.AppendLine();

        // Style Guidelines
        prompt.AppendLine("Style Guidelines:");
        prompt.AppendLine("- Be concise (1-2 sentences per tag, not more)");
        prompt.AppendLine(
            "- Use present tense, third person (e.g., 'Validates input' not 'Validate input' or 'This validates input')");
        prompt.AppendLine("- Focus on WHAT the method does and WHY it's useful, not HOW it works internally");
        prompt.AppendLine("- Avoid redundant phrases like 'This method...' or 'Gets or sets...'");
        prompt.AppendLine("- Use proper XML escaping for special characters (&lt; &gt; &amp;)");
        prompt.AppendLine();

        // Examples of Good Documentation
        prompt.AppendLine("Examples of Good Documentation:");
        prompt.AppendLine();

        prompt.AppendLine("Example 1 (Method with parameters):");
        prompt.AppendLine("<summary>Validates user input against length constraints and content rules.</summary>");
        prompt.AppendLine("<param name=\"input\">The user-provided input string to validate.</param>");
        prompt.AppendLine("<param name=\"maxLength\">Maximum allowed length for the input.</param>");
        prompt.AppendLine("<returns>True if input is valid; otherwise, false.</returns>");
        prompt.AppendLine();

        prompt.AppendLine("Example 2 (Property):");
        prompt.AppendLine("<summary>The configuration settings for LLM provider connections.</summary>");
        prompt.AppendLine();

        prompt.AppendLine(
            "Now generate the XML documentation for the API described above. Return ONLY the XML tags, no additional text or explanations.");

        return prompt.ToString();
    }
}

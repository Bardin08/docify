using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.Models;
using Docify.LLM.PromptEngineering;
using Docify.LLM.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.LLM.Tests.Providers;

/// <summary>
/// Integration tests for GptProvider.
/// These tests use mocked OpenAI responses (no real API calls) to verify end-to-end behavior.
/// </summary>
public class GptProviderIntegrationTests
{
    private readonly Mock<ISecretStore> _mockSecretStore;
    private readonly PromptBuilder _promptBuilder;
    private readonly Mock<ILogger<GptProvider>> _mockLogger;

    public GptProviderIntegrationTests()
    {
        _mockSecretStore = new Mock<ISecretStore>();
        _promptBuilder = new PromptBuilder();
        _mockLogger = new Mock<ILogger<GptProvider>>();

        _mockSecretStore.Setup(s => s.GetApiKey("openai")).ReturnsAsync("test-api-key");
    }

    [Fact]
    public void PromptBuilder_WithSimpleMethod_GeneratesValidPrompt()
    {
        // Arrange
        var context = new ApiContext
        {
            ApiSymbolId = "ValidateInput",
            ParameterTypes = ["string input", "int maxLength"],
            ReturnType = "bool",
            TokenEstimate = 500,
            ImplementationBody = "return input.Length <= maxLength;",
            IsImplementationTruncated = false,
            CallSites = [],
            CalledMethodsDocumentation = [],
            InheritanceHierarchy = [],
            RelatedTypes = [],
            XmlDocComments = string.Empty
        };

        // Act
        var prompt = _promptBuilder.BuildPrompt(context);

        // Assert
        prompt.ShouldNotBeNullOrWhiteSpace();
        prompt.ShouldContain("ValidateInput");
        prompt.ShouldContain("string input");
        prompt.ShouldContain("int maxLength");
        prompt.ShouldContain("bool");
        prompt.ShouldContain("return input.Length <= maxLength;");
        prompt.ShouldContain("Generate XML documentation");
        prompt.ShouldContain("<summary>");
        prompt.ShouldContain("<param>");
        prompt.ShouldContain("<returns>");
    }

    [Fact]
    public void PromptBuilder_WithGenericMethod_IncludesTypeParameters()
    {
        // Arrange
        var context = new ApiContext
        {
            ApiSymbolId = "ProcessItem<T>",
            ParameterTypes = ["T item"],
            ReturnType = "Task<T>",
            TokenEstimate = 400,
            ImplementationBody = "await Task.Delay(100); return item;",
            IsImplementationTruncated = false,
            CallSites = [],
            CalledMethodsDocumentation = [],
            InheritanceHierarchy = [],
            RelatedTypes = ["T"],
            XmlDocComments = string.Empty
        };

        // Act
        var prompt = _promptBuilder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("ProcessItem<T>");
        prompt.ShouldContain("T item");
        prompt.ShouldContain("Task<T>");
        prompt.ShouldContain("Related types: T");
    }

    [Fact]
    public void PromptBuilder_WithUsageExamples_IncludesCallSites()
    {
        // Arrange
        var context = new ApiContext
        {
            ApiSymbolId = "CalculateTotal",
            ParameterTypes = ["decimal[] values"],
            ReturnType = "decimal",
            TokenEstimate = 600,
            ImplementationBody = "return values.Sum();",
            IsImplementationTruncated = false,
            CallSites =
            [
                new CallSiteInfo
                {
                    FilePath = "Program.cs",
                    LineNumber = 42,
                    CallExpression = "var total = CalculateTotal(prices);",
                    ContextBefore = ["// Calculate order total"],
                    ContextAfter = ["Console.WriteLine(total);"]
                }
            ],
            CalledMethodsDocumentation = [],
            InheritanceHierarchy = [],
            RelatedTypes = [],
            XmlDocComments = string.Empty
        };

        // Act
        var prompt = _promptBuilder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Usage Examples:");
        prompt.ShouldContain("Program.cs:42");
        prompt.ShouldContain("CalculateTotal(prices)");
        prompt.ShouldContain("// Calculate order total");
        prompt.ShouldContain("Console.WriteLine(total);");
    }

    [Fact]
    public void PromptBuilder_WithCalledMethods_IncludesDocumentation()
    {
        // Arrange
        var context = new ApiContext
        {
            ApiSymbolId = "ProcessData",
            ParameterTypes = ["string data"],
            ReturnType = "string",
            TokenEstimate = 700,
            ImplementationBody = "var sanitized = SanitizeInput(data); return sanitized.ToUpper();",
            IsImplementationTruncated = false,
            CallSites = [],
            CalledMethodsDocumentation =
            [
                new CalledMethodDoc
                {
                    MethodName = "SanitizeInput",
                    XmlDocumentation =
                        "<summary>Removes potentially harmful characters from user input.</summary>",
                    IsFresh = true
                }
            ],
            InheritanceHierarchy = [],
            RelatedTypes = [],
            XmlDocComments = string.Empty
        };

        // Act
        var prompt = _promptBuilder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Called Methods Documentation:");
        prompt.ShouldContain("SanitizeInput");
        prompt.ShouldContain("Removes potentially harmful characters from user input");
    }

    [Fact]
    public void PromptBuilder_WithProperty_GeneratesAppropriatePrompt()
    {
        // Arrange
        var context = new ApiContext
        {
            ApiSymbolId = "ConnectionString",
            ParameterTypes = [],
            ReturnType = "string",
            TokenEstimate = 300,
            ImplementationBody = "get => _connectionString;",
            IsImplementationTruncated = false,
            CallSites = [],
            CalledMethodsDocumentation = [],
            InheritanceHierarchy = [],
            RelatedTypes = [],
            XmlDocComments = string.Empty
        };

        // Act
        var prompt = _promptBuilder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("ConnectionString");
        prompt.ShouldContain("string");
        prompt.ShouldContain("get => _connectionString;");
    }

    [Fact]
    public void PromptBuilder_WithInheritanceHierarchy_IncludesRelationships()
    {
        // Arrange
        var context = new ApiContext
        {
            ApiSymbolId = "Execute",
            ParameterTypes = [],
            ReturnType = "void",
            TokenEstimate = 500,
            ImplementationBody = "Console.WriteLine(\"Executing...\");",
            IsImplementationTruncated = false,
            CallSites = [],
            CalledMethodsDocumentation = [],
            InheritanceHierarchy = ["BaseCommand", "ICommand"],
            RelatedTypes = [],
            XmlDocComments = string.Empty
        };

        // Act
        var prompt = _promptBuilder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Type Relationships:");
        prompt.ShouldContain("BaseCommand");
        prompt.ShouldContain("ICommand");
    }

    [Fact]
    public void PromptBuilder_WithTruncatedImplementation_IncludesNote()
    {
        // Arrange
        var context = new ApiContext
        {
            ApiSymbolId = "ComplexMethod",
            ParameterTypes = [],
            ReturnType = "void",
            TokenEstimate = 800,
            ImplementationBody = "// Very long implementation...",
            IsImplementationTruncated = true,
            CallSites = [],
            CalledMethodsDocumentation = [],
            InheritanceHierarchy = [],
            RelatedTypes = [],
            XmlDocComments = string.Empty
        };

        // Act
        var prompt = _promptBuilder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("(Implementation truncated for token budget)");
    }

    [Fact]
    public void PromptBuilder_IncludesOutputFormatGuidelines()
    {
        // Arrange
        var context = CreateMinimalContext();

        // Act
        var prompt = _promptBuilder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Output Format:");
        prompt.ShouldContain("valid XML documentation");
        prompt.ShouldContain("<summary> tag (required)");
        prompt.ShouldContain("<param> tags");
        prompt.ShouldContain("<returns> tag");
    }

    [Fact]
    public void PromptBuilder_IncludesStyleGuidelines()
    {
        // Arrange
        var context = CreateMinimalContext();

        // Act
        var prompt = _promptBuilder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Style Guidelines:");
        prompt.ShouldContain("Be concise");
        prompt.ShouldContain("present tense");
        prompt.ShouldContain("third person");
    }

    [Fact]
    public void PromptBuilder_IncludesExamplesOfGoodDocumentation()
    {
        // Arrange
        var context = CreateMinimalContext();

        // Act
        var prompt = _promptBuilder.BuildPrompt(context);

        // Assert
        prompt.ShouldContain("Examples of Good Documentation:");
        prompt.ShouldContain("Validates user input");
        prompt.ShouldContain("configuration settings");
    }

    private static ApiContext CreateMinimalContext()
    {
        return new ApiContext
        {
            ApiSymbolId = "TestMethod",
            ParameterTypes = [],
            ReturnType = "void",
            TokenEstimate = 100,
            ImplementationBody = string.Empty,
            IsImplementationTruncated = false,
            CallSites = [],
            CalledMethodsDocumentation = [],
            InheritanceHierarchy = [],
            RelatedTypes = [],
            XmlDocComments = string.Empty
        };
    }
}

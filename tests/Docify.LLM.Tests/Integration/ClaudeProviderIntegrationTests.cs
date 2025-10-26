using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.PromptEngineering;
using Docify.LLM.Providers;
using Docify.LLM.Validation;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.LLM.Tests.Integration;

/// <summary>
/// Integration tests for ClaudeProvider that verify component integration without making actual API calls.
/// </summary>
public class ClaudeProviderIntegrationTests
{
    [Fact]
    public async Task ClaudeProvider_Integration_WithSecretStore_AndPromptBuilder_WorksTogether()
    {
        // Arrange
        var mockSecretStore = new Mock<ISecretStore>();
        mockSecretStore.Setup(s => s.GetApiKey("anthropic"))
            .ReturnsAsync("test-api-key");

        var promptBuilder = new PromptBuilder();
        var mockLogger = new Mock<ILogger<ClaudeProvider>>();

        var provider = new ClaudeProvider(mockSecretStore.Object, promptBuilder, new OutputValidator(), mockLogger.Object);

        // Act - Verify provider initialization and basic properties
        var providerName = provider.GetProviderName();
        var isAvailable = await provider.IsAvailable();

        // Assert
        providerName.ShouldBe("anthropic");
        isAvailable.ShouldBeTrue();
        mockSecretStore.Verify(s => s.GetApiKey("anthropic"), Times.Once);
    }

    [Fact]
    public void PromptBuilder_Integration_WithCompleteApiContext_GeneratesValidPrompt()
    {
        // Arrange
        var promptBuilder = new PromptBuilder();
        var context = CreateCompleteApiContext();

        // Act
        var prompt = promptBuilder.BuildPrompt(context);

        // Assert - Verify comprehensive prompt structure
        prompt.ShouldNotBeNullOrWhiteSpace();
        prompt.Length.ShouldBeGreaterThan(500); // Comprehensive prompt should be substantial

        // Verify all major sections are included
        prompt.ShouldContain("Generate XML documentation");
        prompt.ShouldContain("API Signature:");
        prompt.ShouldContain("Implementation:");
        prompt.ShouldContain("Called Methods Documentation:");
        prompt.ShouldContain("Type Relationships:");
        prompt.ShouldContain("Usage Examples:");
        prompt.ShouldContain("Related Documentation:");
        prompt.ShouldContain("Output Format:");
        prompt.ShouldContain("Style Guidelines:");
        prompt.ShouldContain("Examples of Good Documentation:");

        // Verify specific context content is included
        prompt.ShouldContain("TestNamespace.Calculator.Divide");
        prompt.ShouldContain("double numerator");
        prompt.ShouldContain("double denominator");
        prompt.ShouldContain("DivideByZeroException");
        prompt.ShouldContain("ValidateInput");
    }

    [Fact]
    public void ClaudeProvider_Integration_EstimateCost_CalculatesCorrectlyWithPromptBuilder()
    {
        // Arrange
        var mockSecretStore = new Mock<ISecretStore>();
        var promptBuilder = new PromptBuilder();
        var mockLogger = new Mock<ILogger<ClaudeProvider>>();
        var provider = new ClaudeProvider(mockSecretStore.Object, promptBuilder, new OutputValidator(), mockLogger.Object);

        var context = CreateCompleteApiContext();

        // Act
        var estimatedCost = provider.EstimateCost(context);

        // Assert
        // Should calculate based on token estimate (5000) + estimated output (500)
        // Input: (5000 / 1,000,000) * 3 = $0.015
        // Output: (500 / 1,000,000) * 15 = $0.0075
        // Total: $0.0225
        estimatedCost.ShouldBe(0.0225m);
    }

    [Fact]
    public async Task ClaudeProvider_Integration_WithMissingApiKey_ThrowsExpectedException()
    {
        // Arrange
        var mockSecretStore = new Mock<ISecretStore>();
        mockSecretStore.Setup(s => s.GetApiKey("anthropic"))
            .ReturnsAsync(string.Empty);

        var promptBuilder = new PromptBuilder();
        var mockLogger = new Mock<ILogger<ClaudeProvider>>();
        var provider = new ClaudeProvider(mockSecretStore.Object, promptBuilder, new OutputValidator(), mockLogger.Object);

        var context = CreateSimpleApiContext();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await provider.GenerateDocumentationAsync(context));

        exception.Message.ShouldContain("API key for anthropic not found");
        exception.Message.ShouldContain("docify config set-api-key");
    }

    [Fact]
    public async Task ClaudeProvider_Integration_IsAvailable_ReflectsSecretStoreState()
    {
        // Arrange
        var mockSecretStore = new Mock<ISecretStore>();
        var promptBuilder = new PromptBuilder();
        var mockLogger = new Mock<ILogger<ClaudeProvider>>();
        var provider = new ClaudeProvider(mockSecretStore.Object, promptBuilder, new OutputValidator(), mockLogger.Object);

        // Act & Assert - Initially no API key
        mockSecretStore.Setup(s => s.GetApiKey("anthropic"))
            .ReturnsAsync(string.Empty);
        var isAvailable1 = await provider.IsAvailable();
        isAvailable1.ShouldBeFalse();

        // Act & Assert - After API key is set
        mockSecretStore.Setup(s => s.GetApiKey("anthropic"))
            .ReturnsAsync("valid-api-key");
        var isAvailable2 = await provider.IsAvailable();
        isAvailable2.ShouldBeTrue();
    }

    [Fact]
    public void PromptBuilder_Integration_WithMinimalContext_GeneratesValidPrompt()
    {
        // Arrange
        var promptBuilder = new PromptBuilder();
        var context = CreateSimpleApiContext();

        // Act
        var prompt = promptBuilder.BuildPrompt(context);

        // Assert
        prompt.ShouldNotBeNullOrWhiteSpace();
        prompt.ShouldContain("Generate XML documentation");
        prompt.ShouldContain("TestNamespace.TestClass.SimpleMethod");
        prompt.ShouldContain("Output Format:");
        prompt.ShouldContain("<summary>");
    }

    [Fact]
    public void PromptBuilder_Integration_WithLargeContext_HandlesGracefully()
    {
        // Arrange
        var promptBuilder = new PromptBuilder();
        var context = CreateLargeApiContext();

        // Act
        var prompt = promptBuilder.BuildPrompt(context);

        // Assert
        prompt.ShouldNotBeNullOrWhiteSpace();
        // Should limit usage examples to 3
        prompt.ShouldContain("Example 1");
        prompt.ShouldContain("Example 2");
        prompt.ShouldContain("Example 3");
        prompt.ShouldNotContain("Example 4");

        // Should exclude related types if > 10
        prompt.ShouldNotContain("Related types:");
    }

    private static ApiContext CreateSimpleApiContext()
    {
        return new ApiContext
        {
            ApiSymbolId = "TestNamespace.TestClass.SimpleMethod",
            ParameterTypes = new List<string>(),
            ReturnType = "void",
            InheritanceHierarchy = new List<string>(),
            RelatedTypes = new List<string>(),
            TokenEstimate = 100,
            CallSites = new List<CallSiteInfo>(),
            CalledMethodsDocumentation = new List<CalledMethodDoc>(),
            IsImplementationTruncated = false
        };
    }

    private static ApiContext CreateCompleteApiContext()
    {
        return new ApiContext
        {
            ApiSymbolId = "TestNamespace.Calculator.Divide",
            ParameterTypes = new List<string> { "double numerator", "double denominator" },
            ReturnType = "double",
            InheritanceHierarchy = new List<string> { "Object", "BaseCalculator", "Calculator" },
            RelatedTypes = new List<string> { "DivideByZeroException", "ArgumentException" },
            TokenEstimate = 5000,
            ImplementationBody = "if (denominator == 0) throw new DivideByZeroException(); return numerator / denominator;",
            CalledMethodsDocumentation = new List<CalledMethodDoc>
            {
                new CalledMethodDoc
                {
                    MethodName = "ValidateInput",
                    XmlDocumentation = "/// <summary>Validates division input parameters.</summary>",
                    IsFresh = true
                }
            },
            CallSites = new List<CallSiteInfo>
            {
                new CallSiteInfo
                {
                    FilePath = "Program.cs",
                    LineNumber = 10,
                    ContextBefore = new List<string> { "var calculator = new Calculator();" },
                    CallExpression = "var result = calculator.Divide(10, 2);",
                    ContextAfter = new List<string> { "Console.WriteLine(result);" }
                }
            },
            XmlDocComments = "/// <summary>Divides two numbers.</summary>",
            IsImplementationTruncated = false
        };
    }

    private static ApiContext CreateLargeApiContext()
    {
        return new ApiContext
        {
            ApiSymbolId = "TestNamespace.LargeClass.ComplexMethod",
            ParameterTypes = new List<string> { "string param1", "int param2", "bool param3" },
            ReturnType = "Task<Result>",
            InheritanceHierarchy = new List<string> { "Object", "BaseClass" },
            RelatedTypes = new List<string>
            {
                "Type1", "Type2", "Type3", "Type4", "Type5",
                "Type6", "Type7", "Type8", "Type9", "Type10",
                "Type11", "Type12" // More than 10
            },
            TokenEstimate = 10000,
            CallSites = new List<CallSiteInfo>
            {
                CreateCallSite("File1.cs", 1),
                CreateCallSite("File2.cs", 2),
                CreateCallSite("File3.cs", 3),
                CreateCallSite("File4.cs", 4),
                CreateCallSite("File5.cs", 5)
            },
            CalledMethodsDocumentation = new List<CalledMethodDoc>(),
            IsImplementationTruncated = false
        };
    }

    private static CallSiteInfo CreateCallSite(string filePath, int lineNumber)
    {
        return new CallSiteInfo
        {
            FilePath = filePath,
            LineNumber = lineNumber,
            ContextBefore = new List<string>(),
            CallExpression = $"Method call at line {lineNumber}",
            ContextAfter = new List<string>()
        };
    }
}

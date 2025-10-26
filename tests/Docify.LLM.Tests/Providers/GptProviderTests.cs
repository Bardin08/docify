using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.Exceptions;
using Docify.LLM.Models;
using Docify.LLM.PromptEngineering;
using Docify.LLM.Providers;
using Docify.LLM.Validation;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.LLM.Tests.Providers;

public class GptProviderTests
{
    private readonly Mock<ISecretStore> _mockSecretStore;
    private readonly PromptBuilder _promptBuilder;
    private readonly Mock<ILogger<GptProvider>> _mockLogger;
    private readonly GptProvider _provider;

    public GptProviderTests()
    {
        _mockSecretStore = new Mock<ISecretStore>();
        _promptBuilder = new PromptBuilder();
        _mockLogger = new Mock<ILogger<GptProvider>>();

        _provider = new GptProvider(
            _mockSecretStore.Object,
            _promptBuilder,
            new OutputValidator(),
            _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullSecretStore_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new GptProvider(null!, _promptBuilder, new OutputValidator(), _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullPromptBuilder_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new GptProvider(_mockSecretStore.Object, null!, new OutputValidator(), _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new GptProvider(_mockSecretStore.Object, _promptBuilder, new OutputValidator(), null!));
    }

    [Fact]
    public async Task GenerateDocumentationAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _provider.GenerateDocumentationAsync(null!));
    }

    [Fact]
    public async Task GenerateDocumentationAsync_WithMissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockSecretStore.Setup(s => s.GetApiKey("openai")).ReturnsAsync(string.Empty);
        var context = CreateTestContext();

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            _provider.GenerateDocumentationAsync(context));

        ex.Message.ShouldContain("API key for openai not found");
        ex.Message.ShouldContain("docify config set-api-key openai");
    }

    [Fact]
    public async Task IsAvailable_WithApiKey_ReturnsTrue()
    {
        // Arrange
        _mockSecretStore.Setup(s => s.GetApiKey("openai")).ReturnsAsync("test-key");

        // Act
        var result = await _provider.IsAvailable();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsAvailable_WithoutApiKey_ReturnsFalse()
    {
        // Arrange
        _mockSecretStore.Setup(s => s.GetApiKey("openai")).ReturnsAsync(string.Empty);

        // Act
        var result = await _provider.IsAvailable();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsAvailable_WithNullApiKey_ReturnsFalse()
    {
        // Arrange
        _mockSecretStore.Setup(s => s.GetApiKey("openai")).ReturnsAsync((string?)null);

        // Act
        var result = await _provider.IsAvailable();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void GetProviderName_ReturnsOpenAI()
    {
        // Act
        var name = _provider.GetProviderName();

        // Assert
        name.ShouldBe("openai");
    }

    [Fact]
    public void EstimateCost_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _provider.EstimateCost(null!));
    }

    [Fact]
    public void EstimateCost_WithValidContext_ReturnsExpectedCost()
    {
        // Arrange
        var context = new ApiContext
        {
            ApiSymbolId = "TestMethod",
            ParameterTypes = ["string param1", "int param2"],
            ReturnType = "bool",
            TokenEstimate = 1000,
            ImplementationBody = "return true;",
            IsImplementationTruncated = false,
            CallSites = [],
            CalledMethodsDocumentation = [],
            InheritanceHierarchy = [],
            RelatedTypes = [],
            XmlDocComments = string.Empty
        };

        // GPT-5-nano pricing: $0.05/M input, $0.40/M output
        // Expected: (1000/1M * 0.05) + (500/1M * 0.40) = 0.00005 + 0.0002 = 0.00025
        var expectedCost = 0.00025m;

        // Act
        var cost = _provider.EstimateCost(context);

        // Assert
        cost.ShouldBe(expectedCost);
    }

    [Fact]
    public void EstimateCost_WithLargeTokenCount_ReturnsCorrectCost()
    {
        // Arrange
        var context = new ApiContext
        {
            ApiSymbolId = "TestMethod",
            ParameterTypes = ["string param1", "int param2"],
            ReturnType = "bool",
            TokenEstimate = 10000,
            ImplementationBody = "return true;",
            IsImplementationTruncated = false,
            CallSites = [],
            CalledMethodsDocumentation = [],
            InheritanceHierarchy = [],
            RelatedTypes = [],
            XmlDocComments = string.Empty
        };

        // Expected: (10000/1M * 0.05) + (500/1M * 0.40) = 0.0005 + 0.0002 = 0.00070
        var expectedCost = 0.00070m;

        // Act
        var cost = _provider.EstimateCost(context);

        // Assert
        cost.ShouldBe(expectedCost);
    }

    [Fact]
    public void EstimateCost_WithZeroTokens_ReturnsOnlyOutputCost()
    {
        // Arrange
        var context = new ApiContext
        {
            ApiSymbolId = "TestMethod",
            ParameterTypes = ["string param1", "int param2"],
            ReturnType = "bool",
            TokenEstimate = 0,
            ImplementationBody = "return true;",
            IsImplementationTruncated = false,
            CallSites = [],
            CalledMethodsDocumentation = [],
            InheritanceHierarchy = [],
            RelatedTypes = [],
            XmlDocComments = string.Empty
        };

        // Expected: (0/1M * 0.05) + (500/1M * 0.40) = 0 + 0.0002 = 0.00020
        var expectedCost = 0.00020m;

        // Act
        var cost = _provider.EstimateCost(context);

        // Assert
        cost.ShouldBe(expectedCost);
    }

    [Fact]
    public async Task GenerateDocumentationAsync_LogsDebugMessages()
    {
        // Arrange
        _mockSecretStore.Setup(s => s.GetApiKey("openai")).ReturnsAsync("invalid-key");
        var context = CreateTestContext();

        // Act - expect exception but verify logging happens before that
        try
        {
            await _provider.GenerateDocumentationAsync(context);
        }
        catch
        {
            // Expected to fail with invalid key
        }

        // Assert - verify that debug logging was called for prompt generation
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Generated prompt")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static ApiContext CreateTestContext()
    {
        return new ApiContext
        {
            ApiSymbolId = "TestMethod",
            ParameterTypes = ["string param1", "int param2"],
            ReturnType = "bool",
            TokenEstimate = 1000,
            ImplementationBody = "return true;",
            IsImplementationTruncated = false,
            CallSites = [],
            CalledMethodsDocumentation = [],
            InheritanceHierarchy = [],
            RelatedTypes = [],
            XmlDocComments = string.Empty
        };
    }
}

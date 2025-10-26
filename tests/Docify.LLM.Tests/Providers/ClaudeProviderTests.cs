using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.PromptEngineering;
using Docify.LLM.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.LLM.Tests.Providers;

public class ClaudeProviderTests
{
    private readonly Mock<ISecretStore> _mockSecretStore;
    private readonly PromptBuilder _promptBuilder;
    private readonly Mock<ILogger<ClaudeProvider>> _mockLogger;
    private readonly ClaudeProvider _provider;

    public ClaudeProviderTests()
    {
        _mockSecretStore = new Mock<ISecretStore>();
        _promptBuilder = new PromptBuilder();
        _mockLogger = new Mock<ILogger<ClaudeProvider>>();

        // Default setup: API key is available
        _mockSecretStore.Setup(s => s.GetApiKey("anthropic"))
            .ReturnsAsync("test-api-key");

        _provider = new ClaudeProvider(_mockSecretStore.Object, _promptBuilder, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullSecretStore_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new ClaudeProvider(null!, _promptBuilder, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullPromptBuilder_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new ClaudeProvider(_mockSecretStore.Object, null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new ClaudeProvider(_mockSecretStore.Object, _promptBuilder, null!));
    }

    [Fact]
    public void GetProviderName_ReturnsAnthropicProvider()
    {
        // Act
        var providerName = _provider.GetProviderName();

        // Assert
        providerName.ShouldBe("anthropic");
    }

    [Fact]
    public async Task IsAvailable_WithApiKeyPresent_ReturnsTrue()
    {
        // Arrange
        _mockSecretStore.Setup(s => s.GetApiKey("anthropic"))
            .ReturnsAsync("valid-api-key");

        // Act
        var isAvailable = await _provider.IsAvailable();

        // Assert
        isAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task IsAvailable_WithNoApiKey_ReturnsFalse()
    {
        // Arrange
        _mockSecretStore.Setup(s => s.GetApiKey("anthropic"))
            .ReturnsAsync(string.Empty);

        // Act
        var isAvailable = await _provider.IsAvailable();

        // Assert
        isAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task GenerateDocumentationAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _provider.GenerateDocumentationAsync(null!));
    }

    [Fact]
    public async Task GenerateDocumentationAsync_WithMissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockSecretStore.Setup(s => s.GetApiKey("anthropic"))
            .ReturnsAsync(string.Empty);

        var context = CreateTestContext();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _provider.GenerateDocumentationAsync(context));

        exception.Message.ShouldContain("API key for anthropic not found");
    }

    [Fact]
    public void EstimateCost_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            _provider.EstimateCost(null!));
    }

    [Fact]
    public void EstimateCost_CalculatesCostCorrectly()
    {
        // Arrange
        var context = CreateTestContext(tokenEstimate: 1000);

        // Act
        var cost = _provider.EstimateCost(context);

        // Assert
        // Claude Sonnet pricing: $3/M input tokens, $15/M output tokens
        // Input cost: (1000 / 1,000,000) * 3 = $0.003
        // Output cost (500 tokens): (500 / 1,000,000) * 15 = $0.0075
        // Total: $0.0105
        cost.ShouldBe(0.0105m);
    }

    [Fact]
    public void EstimateCost_WithLargeTokenCount_CalculatesCorrectly()
    {
        // Arrange
        var context = CreateTestContext(tokenEstimate: 50000);

        // Act
        var cost = _provider.EstimateCost(context);

        // Assert
        // Input cost: (50000 / 1,000,000) * 3 = $0.15
        // Output cost (500 tokens): (500 / 1,000,000) * 15 = $0.0075
        // Total: $0.1575
        cost.ShouldBe(0.1575m);
    }

    private static ApiContext CreateTestContext(int tokenEstimate = 1000)
    {
        return new ApiContext
        {
            ApiSymbolId = "TestNamespace.TestClass.TestMethod",
            ParameterTypes = new List<string>(),
            ReturnType = "void",
            InheritanceHierarchy = new List<string>(),
            RelatedTypes = new List<string>(),
            TokenEstimate = tokenEstimate,
            CallSites = new List<CallSiteInfo>(),
            CalledMethodsDocumentation = new List<CalledMethodDoc>(),
            IsImplementationTruncated = false
        };
    }
}

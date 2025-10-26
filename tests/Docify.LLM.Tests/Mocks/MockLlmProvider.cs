using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.Models;

namespace Docify.LLM.Tests.Mocks;

/// <summary>
/// Mock LLM provider for testing without real API calls.
/// Supports configurable responses and tracks call counts for assertions.
/// </summary>
public class MockLlmProvider : ILlmProvider
{
    private readonly string _providerName;
    private readonly string _generatedXml;
    private readonly bool _isAvailable;
    private readonly Exception? _exceptionToThrow;
    private readonly int _tokenCost;

    public int CallCount { get; private set; }
    public ApiContext? LastContext { get; private set; }

    /// <summary>
    /// Creates a mock provider with successful responses.
    /// </summary>
    public MockLlmProvider(
        string providerName = "mock",
        string generatedXml = "<summary>Test documentation</summary>",
        bool isAvailable = true,
        int tokenCost = 1000)
    {
        _providerName = providerName;
        _generatedXml = generatedXml;
        _isAvailable = isAvailable;
        _tokenCost = tokenCost;
    }

    /// <summary>
    /// Creates a mock provider that throws exceptions.
    /// </summary>
    public MockLlmProvider(Exception exceptionToThrow, string providerName = "mock")
    {
        _providerName = providerName;
        _exceptionToThrow = exceptionToThrow;
        _generatedXml = string.Empty;
        _isAvailable = true;
        _tokenCost = 0;
    }

    public Task<DocumentationSuggestion> GenerateDocumentationAsync(ApiContext context, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastContext = context;

        if (_exceptionToThrow != null)
        {
            throw _exceptionToThrow;
        }

        var suggestion = new DocumentationSuggestion
        {
            Id = Guid.NewGuid().ToString(),
            ApiSymbolId = context.ApiSymbolId,
            GeneratedXml = _generatedXml,
            Provider = _providerName,
            Model = "mock-model",
            TokensUsed = _tokenCost,
            EstimatedCost = EstimateCost(context),
            Timestamp = DateTime.UtcNow,
            Status = SuggestionStatus.Pending,
            UserEditedXml = null
        };

        return Task.FromResult(suggestion);
    }

    public decimal EstimateCost(ApiContext context)
    {
        return 0.01m; // Fixed cost for testing
    }

    public string GetProviderName() => _providerName;

    public Task<bool> IsAvailable() => Task.FromResult(_isAvailable);

    /// <summary>
    /// Resets call tracking for test isolation.
    /// </summary>
    public void Reset()
    {
        CallCount = 0;
        LastContext = null;
    }
}

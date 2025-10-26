using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.Exceptions;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Docify.LLM.Providers;

/// <summary>
/// Factory for creating LLM provider instances with automatic fallback support.
/// </summary>
public class LlmProviderFactory(ISecretStore secretStore, ILoggerFactory loggerFactory)
{
    private readonly ISecretStore _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    private readonly ILogger<LlmProviderFactory> _logger = loggerFactory.CreateLogger<LlmProviderFactory>();

    private readonly Dictionary<string, int> _failureCounters = new();
    private const int MaxConsecutiveFailures = 5;

    private ILlmProvider? _currentProvider;
    private LlmConfiguration? _currentConfig;

    /// <summary>
    /// Creates or returns the current LLM provider based on configuration.
    /// Implements fallback logic: after 5 consecutive failures, prompts user to switch to fallback provider.
    /// </summary>
    /// <param name="config">LLM configuration (primary and optional fallback).</param>
    /// <returns>Active LLM provider instance.</returns>
    /// <exception cref="ProviderUnavailableException">Thrown if no provider is available.</exception>
    public async Task<ILlmProvider> CreateProvider(LlmConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // If config changed, reset current provider
        if (_currentConfig == null || !ConfigEquals(_currentConfig, config))
        {
            _currentConfig = config;
            _currentProvider = CreateProviderInstance(config.PrimaryProvider);
            _failureCounters.Clear();
        }

        // Check if primary provider has too many failures
        var primaryProvider = config.PrimaryProvider;
        if (_failureCounters.TryGetValue(primaryProvider, out var failures) && failures >= MaxConsecutiveFailures)
        {
            _logger.LogWarning(
                "Primary provider {PrimaryProvider} has {Failures} consecutive failures. Checking fallback...",
                primaryProvider, failures);

            // Check if fallback is configured
            if (string.IsNullOrWhiteSpace(config.FallbackProvider))
            {
                throw new ProviderUnavailableException(
                    primaryProvider,
                    $"Primary provider '{primaryProvider}' unavailable after {MaxConsecutiveFailures} consecutive failures and no fallback configured. " +
                    $"Run: docify config set-fallback <provider>");
            }

            // Prompt user to switch to fallback
            var confirmed = AnsiConsole.Confirm(
                $"Primary provider '{primaryProvider}' unavailable. Switch to fallback provider '{config.FallbackProvider}'?",
                defaultValue: true);

            if (confirmed)
            {
                _logger.LogInformation(
                    "Switching from {PrimaryProvider} to fallback provider {FallbackProvider}",
                    primaryProvider, config.FallbackProvider);

                // Switch to fallback provider
                _currentProvider = CreateProviderInstance(config.FallbackProvider);
                _failureCounters.Clear();  // Reset counters
            }
            else
            {
                throw new ProviderUnavailableException(
                    primaryProvider,
                    $"User declined to switch to fallback provider. Cannot continue with unavailable primary provider '{primaryProvider}'.");
            }
        }

        // Verify provider is available
        if (_currentProvider != null && await _currentProvider.IsAvailable())
        {
            return _currentProvider;
        }

        var providerName = _currentProvider?.GetProviderName() ?? primaryProvider;
        throw new InvalidOperationException(
            $"API key for provider '{providerName}' not found. Run: docify config set-api-key {providerName}");
    }

    /// <summary>
    /// Records a successful API call, resetting the failure counter for the provider.
    /// </summary>
    public void RecordSuccess(string providerName)
    {
        ArgumentNullException.ThrowIfNull(providerName);

        if (_failureCounters.ContainsKey(providerName))
        {
            _logger.LogDebug("Resetting failure counter for {ProviderName}", providerName);
            _failureCounters[providerName] = 0;
        }
    }

    /// <summary>
    /// Records a failed API call, incrementing the failure counter for the provider.
    /// </summary>
    public void RecordFailure(string providerName)
    {
        ArgumentNullException.ThrowIfNull(providerName);

        if (!_failureCounters.ContainsKey(providerName))
        {
            _failureCounters[providerName] = 0;
        }

        _failureCounters[providerName]++;
        _logger.LogWarning(
            "Recorded failure for {ProviderName}. Consecutive failures: {FailureCount}",
            providerName, _failureCounters[providerName]);
    }

    private ILlmProvider CreateProviderInstance(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "anthropic" => new ClaudeProvider(_secretStore, _loggerFactory.CreateLogger<ClaudeProvider>()),
            "openai" => new GptProvider(_secretStore, _loggerFactory.CreateLogger<GptProvider>()),
            _ => throw new ConfigurationException($"Unknown provider '{providerName}'. Supported: anthropic, openai")
        };
    }

    private static bool ConfigEquals(LlmConfiguration a, LlmConfiguration b)
    {
        return a.PrimaryProvider == b.PrimaryProvider &&
               a.PrimaryModel == b.PrimaryModel &&
               a.FallbackProvider == b.FallbackProvider &&
               a.FallbackModel == b.FallbackModel;
    }
}

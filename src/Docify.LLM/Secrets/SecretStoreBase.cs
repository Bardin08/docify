using Docify.LLM.Abstractions;
using Microsoft.Extensions.Logging;

namespace Docify.LLM.Secrets;

/// <summary>
/// Base class for platform-specific secret stores with common functionality.
/// </summary>
public abstract class SecretStoreBase(ILogger logger) : ISecretStore
{
    protected ILogger Logger { get; } = logger;

    protected static string GetServiceName(string provider) => $"Docify.{provider}";
    protected static string GetEnvironmentVariableName(string provider) => $"DOCIFY_API_KEY_{provider.ToUpperInvariant()}";

    public abstract Task SaveApiKey(string provider, string apiKey);
    public abstract Task DeleteApiKey(string provider);
    protected abstract Task<string?> GetApiKeyFromKeychain(string provider);

    public async Task<string?> GetApiKey(string provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        // Try keychain first
        try
        {
            var key = await GetApiKeyFromKeychain(provider);
            if (!string.IsNullOrWhiteSpace(key))
            {
                Logger.LogDebug("Retrieved API key for {Provider} from OS keychain", provider);
                return key;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to retrieve API key for {Provider} from keychain. Falling back to environment variable.", provider);
        }

        // Fall back to environment variable
        var envVar = GetEnvironmentVariableName(provider);
        var envKey = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            Logger.LogDebug("Retrieved API key for {Provider} from environment variable {EnvVar}", provider, envVar);
            return envKey;
        }

        Logger.LogDebug("No API key found for {Provider} in keychain or environment variables", provider);
        return null;
    }

    public string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return "****";

        if (apiKey.Length <= 4)
            return "****";

        return "****" + apiKey[^4..];
    }
}

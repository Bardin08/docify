namespace Docify.LLM.Abstractions;

/// <summary>
/// Interface for securely storing and retrieving API keys using OS-native credential storage.
/// </summary>
public interface ISecretStore
{
    /// <summary>
    /// Saves an API key for the specified provider to OS-native keychain.
    /// </summary>
    /// <param name="provider">Provider name (e.g., "anthropic", "openai").</param>
    /// <param name="apiKey">API key to store securely.</param>
    /// <exception cref="InvalidOperationException">Thrown if keychain is unavailable.</exception>
    Task SaveApiKey(string provider, string apiKey);

    /// <summary>
    /// Retrieves an API key for the specified provider from OS-native keychain or environment variables.
    /// Falls back to environment variable DOCIFY_API_KEY_{PROVIDER} (uppercase) if keychain fails.
    /// </summary>
    /// <param name="provider">Provider name (e.g., "anthropic", "openai").</param>
    /// <returns>API key if found; otherwise null.</returns>
    Task<string?> GetApiKey(string provider);

    /// <summary>
    /// Deletes an API key for the specified provider from OS-native keychain.
    /// </summary>
    /// <param name="provider">Provider name (e.g., "anthropic", "openai").</param>
    Task DeleteApiKey(string provider);

    /// <summary>
    /// Masks an API key for safe display in logs or UI (shows last 4 characters only).
    /// </summary>
    /// <param name="apiKey">API key to mask.</param>
    /// <returns>Masked key (e.g., "****abcd").</returns>
    string MaskApiKey(string? apiKey);
}

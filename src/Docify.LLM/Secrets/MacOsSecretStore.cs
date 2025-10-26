using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Docify.LLM.Secrets;

/// <summary>
/// macOS-specific secret store using Keychain Services.
/// Uses P/Invoke to interact with the native Security framework.
/// </summary>
public class MacOsSecretStore(ILogger<MacOsSecretStore> logger) : SecretStoreBase(logger)
{
    private const string ServiceName = "Docify";

    public override async Task SaveApiKey(string provider, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(apiKey);

        try
        {
            // Delete existing key if present
            await DeleteApiKey(provider).ConfigureAwait(false);

            var serviceName = $"{ServiceName}.{provider}";
            var passwordBytes = Encoding.UTF8.GetBytes(apiKey);

            var status = SecKeychainAddGenericPassword(
                IntPtr.Zero,                    // Use default keychain
                (uint)serviceName.Length,
                serviceName,
                (uint)provider.Length,
                provider,
                (uint)passwordBytes.Length,
                passwordBytes,
                out _);

            if (status != 0)
            {
                Logger.LogWarning(
                    "Failed to save API key to macOS Keychain (error {Status}). Falling back to environment variable: {EnvVar}",
                    status, GetEnvironmentVariableName(provider));
            }
            else
            {
                Logger.LogInformation("API key for {Provider} saved to macOS Keychain", provider);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex,
                "Exception while saving to macOS Keychain. Falling back to environment variable: {EnvVar}",
                GetEnvironmentVariableName(provider));
        }
    }

    public override Task DeleteApiKey(string provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        try
        {
            var serviceName = $"{ServiceName}.{provider}";

            // First, find the item
            var findStatus = SecKeychainFindGenericPassword(
                IntPtr.Zero,
                (uint)serviceName.Length,
                serviceName,
                (uint)provider.Length,
                provider,
                out _,
                out _,
                out var itemRef);

            if (findStatus == 0 && itemRef != IntPtr.Zero)
            {
                // Delete the item
                var deleteStatus = SecKeychainItemDelete(itemRef);
                CFRelease(itemRef);

                if (deleteStatus == 0)
                {
                    Logger.LogInformation("API key for {Provider} deleted from macOS Keychain", provider);
                }
                else
                {
                    Logger.LogWarning("Failed to delete API key from macOS Keychain (error {Status})", deleteStatus);
                }
            }
            else if (findStatus == -25300) // errSecItemNotFound
            {
                Logger.LogDebug("No API key found in macOS Keychain for {Provider}", provider);
            }
            else
            {
                Logger.LogWarning("Failed to find API key in macOS Keychain (error {Status})", findStatus);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Exception while deleting from macOS Keychain");
        }

        return Task.CompletedTask;
    }

    protected override Task<string?> GetApiKeyFromKeychain(string provider)
    {
        try
        {
            var serviceName = $"{ServiceName}.{provider}";

            var status = SecKeychainFindGenericPassword(
                IntPtr.Zero,
                (uint)serviceName.Length,
                serviceName,
                (uint)provider.Length,
                provider,
                out var passwordLength,
                out var passwordData,
                out var itemRef);

            if (status == 0 && passwordData != IntPtr.Zero)
            {
                try
                {
                    var passwordBytes = new byte[passwordLength];
                    Marshal.Copy(passwordData, passwordBytes, 0, (int)passwordLength);
                    var password = Encoding.UTF8.GetString(passwordBytes);

                    Logger.LogDebug("API key for {Provider} retrieved from macOS Keychain", provider);
                    return Task.FromResult<string?>(password);
                }
                finally
                {
                    // Free the password memory
                    SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
                    if (itemRef != IntPtr.Zero)
                    {
                        CFRelease(itemRef);
                    }
                }
            }
            else if (status == -25300) // errSecItemNotFound
            {
                Logger.LogDebug("No API key found in macOS Keychain for {Provider}", provider);
            }
            else
            {
                Logger.LogWarning("Failed to retrieve API key from macOS Keychain (error {Status})", status);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Exception while reading from macOS Keychain");
        }

        return Task.FromResult<string?>(null);
    }

    #region P/Invoke Declarations

    [DllImport("/System/Library/Frameworks/Security.framework/Security", CharSet = CharSet.Ansi)]
    private static extern int SecKeychainAddGenericPassword(
        IntPtr keychain,
        uint serviceNameLength,
        string serviceName,
        uint accountNameLength,
        string accountName,
        uint passwordLength,
        byte[] passwordData,
        out IntPtr itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security", CharSet = CharSet.Ansi)]
    private static extern int SecKeychainFindGenericPassword(
        IntPtr keychain,
        uint serviceNameLength,
        string serviceName,
        uint accountNameLength,
        string accountName,
        out uint passwordLength,
        out IntPtr passwordData,
        out IntPtr itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemDelete(IntPtr itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    #endregion
}

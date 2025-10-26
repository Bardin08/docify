using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Docify.LLM.Secrets;

/// <summary>
/// Linux-specific secret store using Secret Service API (libsecret).
/// Uses P/Invoke to interact with the libsecret library.
/// </summary>
public class LinuxSecretStore(ILogger<LinuxSecretStore> logger) : SecretStoreBase(logger)
{
    private const string Schema = "com.docify.ApiKey";
    private const string AttributeProvider = "provider";

    public override Task SaveApiKey(string provider, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(apiKey);

        try
        {
            var schema = CreateSchema();
            if (schema == IntPtr.Zero)
            {
                Logger.LogWarning("Failed to create libsecret schema. Falling back to environment variable: {EnvVar}",
                    GetEnvironmentVariableName(provider));
                return Task.CompletedTask;
            }

            try
            {
                var label = $"Docify API Key for {provider}";
                IntPtr error = IntPtr.Zero;

                var result = secret_password_store_sync(
                    schema,
                    SecretCollection.DEFAULT,
                    label,
                    apiKey,
                    IntPtr.Zero,
                    ref error,
                    AttributeProvider,
                    provider,
                    IntPtr.Zero);

                if (error != IntPtr.Zero)
                {
                    var errorMessage = Marshal.PtrToStringAnsi(g_error_get_message(error));
                    g_error_free(error);
                    Logger.LogWarning("Failed to save API key to Linux Secret Service: {Error}. Falling back to environment variable: {EnvVar}",
                        errorMessage, GetEnvironmentVariableName(provider));
                }
                else if (result)
                {
                    Logger.LogInformation("API key for {Provider} saved to Linux Secret Service", provider);
                }
                else
                {
                    Logger.LogWarning("Failed to save API key to Linux Secret Service. Falling back to environment variable: {EnvVar}",
                        GetEnvironmentVariableName(provider));
                }
            }
            finally
            {
                secret_schema_unref(schema);
            }
        }
        catch (DllNotFoundException)
        {
            Logger.LogWarning("libsecret not found on this system. Falling back to environment variable: {EnvVar}",
                GetEnvironmentVariableName(provider));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex,
                "Exception while saving to Linux Secret Service. Falling back to environment variable: {EnvVar}",
                GetEnvironmentVariableName(provider));
        }

        return Task.CompletedTask;
    }

    public override Task DeleteApiKey(string provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        try
        {
            var schema = CreateSchema();
            if (schema == IntPtr.Zero)
            {
                Logger.LogWarning("Failed to create libsecret schema");
                return Task.CompletedTask;
            }

            try
            {
                IntPtr error = IntPtr.Zero;

                var result = secret_password_clear_sync(
                    schema,
                    IntPtr.Zero,
                    ref error,
                    AttributeProvider,
                    provider,
                    IntPtr.Zero);

                if (error != IntPtr.Zero)
                {
                    var errorMessage = Marshal.PtrToStringAnsi(g_error_get_message(error));
                    g_error_free(error);
                    Logger.LogWarning("Failed to delete API key from Linux Secret Service: {Error}", errorMessage);
                }
                else if (result)
                {
                    Logger.LogInformation("API key for {Provider} deleted from Linux Secret Service", provider);
                }
                else
                {
                    Logger.LogDebug("No API key found in Linux Secret Service for {Provider}", provider);
                }
            }
            finally
            {
                secret_schema_unref(schema);
            }
        }
        catch (DllNotFoundException)
        {
            Logger.LogWarning("libsecret not found on this system");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Exception while deleting from Linux Secret Service");
        }

        return Task.CompletedTask;
    }

    protected override Task<string?> GetApiKeyFromKeychain(string provider)
    {
        try
        {
            var schema = CreateSchema();
            if (schema == IntPtr.Zero)
            {
                Logger.LogWarning("Failed to create libsecret schema");
                return Task.FromResult<string?>(null);
            }

            try
            {
                IntPtr error = IntPtr.Zero;

                var passwordPtr = secret_password_lookup_sync(
                    schema,
                    IntPtr.Zero,
                    ref error,
                    AttributeProvider,
                    provider,
                    IntPtr.Zero);

                if (error != IntPtr.Zero)
                {
                    var errorMessage = Marshal.PtrToStringAnsi(g_error_get_message(error));
                    g_error_free(error);
                    Logger.LogWarning("Failed to retrieve API key from Linux Secret Service: {Error}", errorMessage);
                }
                else if (passwordPtr != IntPtr.Zero)
                {
                    try
                    {
                        var password = Marshal.PtrToStringAnsi(passwordPtr);
                        Logger.LogDebug("API key for {Provider} retrieved from Linux Secret Service", provider);
                        return Task.FromResult<string?>(password);
                    }
                    finally
                    {
                        secret_password_free(passwordPtr);
                    }
                }
                else
                {
                    Logger.LogDebug("No API key found in Linux Secret Service for {Provider}", provider);
                }
            }
            finally
            {
                secret_schema_unref(schema);
            }
        }
        catch (DllNotFoundException)
        {
            Logger.LogDebug("libsecret not found on this system. Checking environment variable instead.");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Exception while reading from Linux Secret Service");
        }

        return Task.FromResult<string?>(null);
    }

    private static IntPtr CreateSchema()
    {
        try
        {
            return secret_schema_new(
                Schema,
                SecretSchemaFlags.NONE,
                AttributeProvider,
                SecretSchemaAttributeType.STRING,
                IntPtr.Zero);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    #region P/Invoke Declarations

    private const string LibSecret = "libsecret-1.so.0";

    private static class SecretCollection
    {
        public const string DEFAULT = "default";
    }

    private enum SecretSchemaFlags
    {
        NONE = 0
    }

    private enum SecretSchemaAttributeType
    {
        STRING = 0,
        INTEGER = 1,
        BOOLEAN = 2
    }

    [DllImport(LibSecret, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr secret_schema_new(
        string name,
        SecretSchemaFlags flags,
        string attribute1Name,
        SecretSchemaAttributeType attribute1Type,
        IntPtr terminator);

    [DllImport(LibSecret, CallingConvention = CallingConvention.Cdecl)]
    private static extern void secret_schema_unref(IntPtr schema);

    [DllImport(LibSecret, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern bool secret_password_store_sync(
        IntPtr schema,
        string collection,
        string label,
        string password,
        IntPtr cancellable,
        ref IntPtr error,
        string attribute1Name,
        string attribute1Value,
        IntPtr terminator);

    [DllImport(LibSecret, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr secret_password_lookup_sync(
        IntPtr schema,
        IntPtr cancellable,
        ref IntPtr error,
        string attribute1Name,
        string attribute1Value,
        IntPtr terminator);

    [DllImport(LibSecret, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern bool secret_password_clear_sync(
        IntPtr schema,
        IntPtr cancellable,
        ref IntPtr error,
        string attribute1Name,
        string attribute1Value,
        IntPtr terminator);

    [DllImport(LibSecret, CallingConvention = CallingConvention.Cdecl)]
    private static extern void secret_password_free(IntPtr password);

    [DllImport("libglib-2.0.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr g_error_get_message(IntPtr error);

    [DllImport("libglib-2.0.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern void g_error_free(IntPtr error);

    #endregion
}

using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Docify.LLM.Secrets;

/// <summary>
/// Windows-specific secret store using Credential Manager.
/// Uses P/Invoke to interact with the Windows Credential Manager API.
/// </summary>
public class WindowsSecretStore(ILogger<WindowsSecretStore> logger) : SecretStoreBase(logger)
{
    private const string TargetNamePrefix = "Docify";

    public override Task SaveApiKey(string provider, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(apiKey);

        try
        {
            var targetName = $"{TargetNamePrefix}.{provider}";
            var credential = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = targetName,
                CredentialBlobSize = (uint)Encoding.Unicode.GetByteCount(apiKey),
                CredentialBlob = Marshal.StringToCoTaskMemUni(apiKey),
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                UserName = provider
            };

            try
            {
                var result = CredWrite(ref credential, 0);
                if (result)
                {
                    Logger.LogInformation("API key for {Provider} saved to Windows Credential Manager", provider);
                }
                else
                {
                    var error = Marshal.GetLastWin32Error();
                    Logger.LogWarning(
                        "Failed to save API key to Windows Credential Manager (error {Error}). Falling back to environment variable: {EnvVar}",
                        error, GetEnvironmentVariableName(provider));
                }
            }
            finally
            {
                if (credential.CredentialBlob != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(credential.CredentialBlob);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex,
                "Exception while saving to Windows Credential Manager. Falling back to environment variable: {EnvVar}",
                GetEnvironmentVariableName(provider));
        }

        return Task.CompletedTask;
    }

    public override Task DeleteApiKey(string provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        try
        {
            var targetName = $"{TargetNamePrefix}.{provider}";
            var result = CredDelete(targetName, CRED_TYPE_GENERIC, 0);

            if (result)
            {
                Logger.LogInformation("API key for {Provider} deleted from Windows Credential Manager", provider);
            }
            else
            {
                var error = Marshal.GetLastWin32Error();
                if (error == ERROR_NOT_FOUND)
                {
                    Logger.LogDebug("No API key found in Windows Credential Manager for {Provider}", provider);
                }
                else
                {
                    Logger.LogWarning("Failed to delete API key from Windows Credential Manager (error {Error})", error);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Exception while deleting from Windows Credential Manager");
        }

        return Task.CompletedTask;
    }

    protected override Task<string?> GetApiKeyFromKeychain(string provider)
    {
        try
        {
            var targetName = $"{TargetNamePrefix}.{provider}";
            var result = CredRead(targetName, CRED_TYPE_GENERIC, 0, out var credPtr);

            if (result && credPtr != IntPtr.Zero)
            {
                try
                {
                    var credential = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                    if (credential.CredentialBlob != IntPtr.Zero && credential.CredentialBlobSize > 0)
                    {
                        var password = Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
                        Logger.LogDebug("API key for {Provider} retrieved from Windows Credential Manager", provider);
                        return Task.FromResult<string?>(password);
                    }
                }
                finally
                {
                    CredFree(credPtr);
                }
            }
            else
            {
                var error = Marshal.GetLastWin32Error();
                if (error == ERROR_NOT_FOUND)
                {
                    Logger.LogDebug("No API key found in Windows Credential Manager for {Provider}", provider);
                }
                else
                {
                    Logger.LogWarning("Failed to retrieve API key from Windows Credential Manager (error {Error})", error);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Exception while reading from Windows Credential Manager");
        }

        return Task.FromResult<string?>(null);
    }

    #region P/Invoke Declarations

    private const int CRED_TYPE_GENERIC = 1;
    private const int CRED_PERSIST_LOCAL_MACHINE = 2;
    private const int ERROR_NOT_FOUND = 1168;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref CREDENTIAL credential, [In] int flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(
        string targetName,
        int type,
        int flags,
        out IntPtr credentialPtr);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(
        string targetName,
        int type,
        int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr credential);

    #endregion
}

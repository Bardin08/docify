using Docify.LLM.Abstractions;
using Microsoft.Extensions.Logging;

namespace Docify.LLM.Secrets;

/// <summary>
/// Factory for creating platform-specific secret store implementations.
/// </summary>
public static class SecretStoreFactory
{
    /// <summary>
    /// Creates the appropriate secret store for the current platform.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating platform-specific loggers.</param>
    /// <returns>Platform-specific secret store implementation.</returns>
    public static ISecretStore GetPlatformSecretStore(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        if (OperatingSystem.IsWindows())
            return new WindowsSecretStore(loggerFactory.CreateLogger<WindowsSecretStore>());

        if (OperatingSystem.IsMacOS())
            return new MacOsSecretStore(loggerFactory.CreateLogger<MacOsSecretStore>());

        if (OperatingSystem.IsLinux())
            return new LinuxSecretStore(loggerFactory.CreateLogger<LinuxSecretStore>());

        throw new PlatformNotSupportedException(
            $"Operating system not supported for secret storage. " +
            $"Please use environment variables: DOCIFY_API_KEY_ANTHROPIC, DOCIFY_API_KEY_OPENAI");
    }
}

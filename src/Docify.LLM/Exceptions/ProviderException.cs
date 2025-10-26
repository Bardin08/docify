namespace Docify.LLM.Exceptions;

/// <summary>
/// Exception thrown when an LLM provider encounters an error during operation.
/// </summary>
public class ProviderException : Exception
{
    /// <summary>
    /// Name of the provider that encountered the error.
    /// </summary>
    public string ProviderName { get; }

    public ProviderException(string providerName, string message)
        : base(message)
    {
        ProviderName = providerName;
    }

    public ProviderException(string providerName, string message, Exception innerException)
        : base(message, innerException)
    {
        ProviderName = providerName;
    }
}

/// <summary>
/// Exception thrown when a provider is unavailable and no fallback is configured.
/// </summary>
public class ProviderUnavailableException : ProviderException
{
    public ProviderUnavailableException(string providerName, string message)
        : base(providerName, message)
    {
    }

    public ProviderUnavailableException(string providerName, string message, Exception innerException)
        : base(providerName, message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when configuration is invalid or missing.
/// </summary>
public class ConfigurationException : Exception
{
    public ConfigurationException(string message)
        : base(message)
    {
    }

    public ConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

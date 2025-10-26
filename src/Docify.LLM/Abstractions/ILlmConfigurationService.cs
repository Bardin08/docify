using Docify.Core.Models;

namespace Docify.LLM.Abstractions;

/// <summary>
/// Service for loading LLM provider configuration from ~/.docify/config.yaml or environment variables.
/// </summary>
public interface ILlmConfigurationService
{
    /// <summary>
    /// Loads the LLM configuration from config file or environment variables.
    /// Environment variables take precedence over file configuration.
    /// Creates a default configuration file if missing.
    /// </summary>
    /// <returns>Validated LLM configuration.</returns>
    /// <exception cref="Exceptions.ConfigurationException">Thrown if configuration is invalid.</exception>
    Task<LlmConfiguration> LoadConfiguration();

    /// <summary>
    /// Saves or updates the configuration file at ~/.docify/config.yaml.
    /// </summary>
    /// <param name="configuration">Configuration to save.</param>
    Task SaveConfiguration(LlmConfiguration configuration);
}

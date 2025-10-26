using Docify.Core.Models;
using Docify.LLM.Abstractions;
using Docify.LLM.Exceptions;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Docify.LLM.Configuration;

/// <summary>
/// Service for loading and saving LLM configuration from ~/.docify/config.yaml and environment variables.
/// </summary>
public class LlmConfigurationService(ILogger<LlmConfigurationService> logger) : ILlmConfigurationService
{
    private static readonly string _configDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".docify");

    private static readonly string _configFilePath = Path.Combine(_configDirectory, "config.yaml");

    /// <inheritdoc/>
    public async Task<LlmConfiguration> LoadConfiguration()
    {
        LlmConfiguration? fileConfig = null;

        // Try to load from file
        if (File.Exists(_configFilePath))
        {
            try
            {
                var yaml = await File.ReadAllTextAsync(_configFilePath);
                fileConfig = ParseYamlConfiguration(yaml);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to load configuration from {ConfigFilePath}. Using environment variables or defaults.",
                    _configFilePath);
            }
        }
        else
        {
            logger.LogInformation("Configuration file not found at {ConfigFilePath}. Creating default configuration.",
                _configFilePath);
            await CreateDefaultConfiguration();
            fileConfig = ParseYamlConfiguration(await File.ReadAllTextAsync(_configFilePath));
        }

        // Apply environment variable overrides (env vars take precedence)
        var primaryProvider = Environment.GetEnvironmentVariable("DOCIFY_PROVIDER") ?? fileConfig?.PrimaryProvider;
        var primaryModel = Environment.GetEnvironmentVariable("DOCIFY_MODEL") ?? fileConfig?.PrimaryModel;
        var fallbackProvider = Environment.GetEnvironmentVariable("DOCIFY_FALLBACK_PROVIDER") ??
                               fileConfig?.FallbackProvider;
        var fallbackModel = Environment.GetEnvironmentVariable("DOCIFY_FALLBACK_MODEL") ?? fileConfig?.FallbackModel;

        if (string.IsNullOrWhiteSpace(primaryProvider) || string.IsNullOrWhiteSpace(primaryModel))
        {
            throw new ConfigurationException(
                "Primary provider and model must be configured. " +
                "Set via ~/.docify/config.yaml or environment variables: DOCIFY_PROVIDER, DOCIFY_MODEL");
        }

        var config = new LlmConfiguration
        {
            PrimaryProvider = primaryProvider,
            PrimaryModel = primaryModel,
            FallbackProvider = fallbackProvider,
            FallbackModel = fallbackModel
        };

        // Validate configuration
        try
        {
            config.Validate();
        }
        catch (ArgumentException ex)
        {
            throw new ConfigurationException($"Invalid configuration: {ex.Message}", ex);
        }

        logger.LogDebug(
            "Loaded configuration: PrimaryProvider={PrimaryProvider}, PrimaryModel={PrimaryModel}, FallbackProvider={FallbackProvider}",
            config.PrimaryProvider, config.PrimaryModel, config.FallbackProvider ?? "none");

        return config;
    }

    /// <inheritdoc/>
    public async Task SaveConfiguration(LlmConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            configuration.Validate();
        }
        catch (ArgumentException ex)
        {
            throw new ConfigurationException($"Cannot save invalid configuration: {ex.Message}", ex);
        }

        // Ensure directory exists
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }

        // Serialize to YAML
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var yamlObject = new
        {
            llm = new
            {
                primary_provider = configuration.PrimaryProvider,
                primary_model = configuration.PrimaryModel,
                fallback_provider = configuration.FallbackProvider,
                fallback_model = configuration.FallbackModel
            }
        };

        var yaml = serializer.Serialize(yamlObject);

        // Write atomically (temp file + move)
        var tempPath = _configFilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, yaml);
        File.Move(tempPath, _configFilePath, overwrite: true);

        logger.LogInformation("Saved configuration to {ConfigFilePath}", _configFilePath);
    }

    private static LlmConfiguration? ParseYamlConfiguration(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var yamlObject = deserializer.Deserialize<Dictionary<string, Dictionary<string, string?>>>(yaml);

        if (!yamlObject.TryGetValue("llm", out var llmConfig))
        {
            return null;
        }

        llmConfig.TryGetValue("primary_provider", out var primaryProvider);
        llmConfig.TryGetValue("primary_model", out var primaryModel);
        llmConfig.TryGetValue("fallback_provider", out var fallbackProvider);
        llmConfig.TryGetValue("fallback_model", out var fallbackModel);

        if (string.IsNullOrWhiteSpace(primaryProvider) || string.IsNullOrWhiteSpace(primaryModel))
        {
            return null;
        }

        return new LlmConfiguration
        {
            PrimaryProvider = primaryProvider,
            PrimaryModel = primaryModel,
            FallbackProvider = fallbackProvider,
            FallbackModel = fallbackModel
        };
    }

    private async Task CreateDefaultConfiguration()
    {
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }

        const string defaultYaml =
            """
            # Docify LLM Configuration
            # Provider settings for documentation generation

            llm:
              # Primary LLM provider (required)
              # Supported: anthropic, openai
              primary_provider: "anthropic"

              # Primary model name (required)
              # Examples: claude-sonnet-4-5, gpt-5-nano
              primary_model: "claude-sonnet-4-5"

              # Optional fallback provider (activated after 5 consecutive primary failures)
              # fallback_provider: "openai"
              # fallback_model: "gpt-5-nano"

            # Environment variable overrides (higher priority):
            # - DOCIFY_PROVIDER
            # - DOCIFY_MODEL
            # - DOCIFY_FALLBACK_PROVIDER
            # - DOCIFY_FALLBACK_MODEL

            # API keys are stored securely in your OS keychain, not in this file.
            # Set API keys using: docify config set-api-key <provider>
            """;

        await File.WriteAllTextAsync(_configFilePath, defaultYaml);
        logger.LogInformation("Created default configuration at {ConfigFilePath}", _configFilePath);
    }
}

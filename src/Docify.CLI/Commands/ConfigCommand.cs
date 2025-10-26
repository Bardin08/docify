using System.CommandLine;
using Docify.LLM.Abstractions;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Docify.CLI.Commands;

/// <summary>
/// CLI command for managing Docify configuration (providers, models, API keys).
/// Supports: set-provider, set-fallback, set-api-key, show.
/// </summary>
public class ConfigCommand : Command
{
    private static readonly string[] _supportedProviders = ["anthropic", "openai"];

    /// <summary>
    /// Creates the config command with all subcommands.
    /// </summary>
    public ConfigCommand(
        ILlmConfigurationService configService,
        ISecretStore secretStore,
        ILogger<ConfigCommand> logger) : base("config", "Manage Docify configuration (providers, models, API keys)")
    {
        // Subcommand: set-provider
        var setProviderCommand = new Command("set-provider", "Set the primary LLM provider");
        var providerArg = new Argument<string>("provider", "Provider name (anthropic, openai)");
        setProviderCommand.AddArgument(providerArg);
        setProviderCommand.SetHandler(async provider =>
        {
            await SetProvider(configService, logger, provider);
        }, providerArg);

        // Subcommand: set-fallback
        var setFallbackCommand = new Command("set-fallback", "Set the fallback LLM provider");
        var fallbackProviderArg = new Argument<string>("provider", "Fallback provider name (anthropic, openai)");
        setFallbackCommand.AddArgument(fallbackProviderArg);
        setFallbackCommand.SetHandler(async provider =>
        {
            await SetFallback(configService, logger, provider);
        }, fallbackProviderArg);

        // Subcommand: set-api-key
        var setApiKeyCommand = new Command("set-api-key", "Set API key for a provider (secure input)");
        var apiKeyProviderArg = new Argument<string>("provider", "Provider name (anthropic, openai)");
        setApiKeyCommand.AddArgument(apiKeyProviderArg);
        setApiKeyCommand.SetHandler(async provider =>
        {
            await SetApiKey(secretStore, logger, provider);
        }, apiKeyProviderArg);

        // Subcommand: show
        var showCommand = new Command("show", "Display current configuration (masks API keys)");
        showCommand.SetHandler(async () =>
        {
            await ShowConfiguration(configService, secretStore, logger);
        });

        AddCommand(setProviderCommand);
        AddCommand(setFallbackCommand);
        AddCommand(setApiKeyCommand);
        AddCommand(showCommand);
    }

    private static async Task SetProvider(ILlmConfigurationService configService, ILogger logger, string provider)
    {
        provider = provider.ToLowerInvariant();

        if (!_supportedProviders.Contains(provider))
        {
            AnsiConsole.MarkupLine(
                $"[red]Error:[/] Unknown provider '{provider}'. Supported: {string.Join(", ", _supportedProviders)}");
            Environment.Exit(1);
            return;
        }

        // Load current config
        var config = await configService.LoadConfiguration();

        // Prompt for model
        var model = AnsiConsole.Ask<string>(
            $"Enter model name for {provider} (e.g., claude-sonnet-4-5, gpt-5-nano):");

        // Update and save
        var updatedConfig = config with { PrimaryProvider = provider, PrimaryModel = model };

        await configService.SaveConfiguration(updatedConfig);

        AnsiConsole.MarkupLine($"[green]✓[/] Primary provider set to: {provider} ({model})");
        logger.LogInformation("Primary provider updated: {Provider} ({Model})", provider, model);
    }

    private static async Task SetFallback(ILlmConfigurationService configService, ILogger logger, string provider)
    {
        provider = provider.ToLowerInvariant();

        if (!_supportedProviders.Contains(provider))
        {
            AnsiConsole.MarkupLine(
                $"[red]Error:[/] Unknown provider '{provider}'. Supported: {string.Join(", ", _supportedProviders)}");
            Environment.Exit(1);
            return;
        }

        // Load current config
        var config = await configService.LoadConfiguration();

        // Prompt for model
        var model = AnsiConsole.Ask<string>(
            $"Enter fallback model name for {provider} (e.g., claude-sonnet-4-5, gpt-5-nano):");

        // Update and save
        var updatedConfig = config with { FallbackProvider = provider, FallbackModel = model };

        await configService.SaveConfiguration(updatedConfig);

        AnsiConsole.MarkupLine($"[green]✓[/] Fallback provider set to: {provider} ({model})");
        logger.LogInformation("Fallback provider updated: {Provider} ({Model})", provider, model);
    }

    private static async Task SetApiKey(ISecretStore secretStore, ILogger logger, string provider)
    {
        provider = provider.ToLowerInvariant();

        if (!_supportedProviders.Contains(provider))
        {
            AnsiConsole.MarkupLine(
                $"[red]Error:[/] Unknown provider '{provider}'. Supported: {string.Join(", ", _supportedProviders)}");
            Environment.Exit(1);
            return;
        }

        // Prompt for API key (secure input, no echo)
        var apiKey = AnsiConsole.Prompt(
            new TextPrompt<string>($"Enter API key for {provider}:")
                .PromptStyle("yellow")
                .Secret());

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] API key cannot be empty");
            Environment.Exit(1);
            return;
        }

        // Save to OS keychain (or log warning about MVP limitation)
        await secretStore.SaveApiKey(provider, apiKey);

        var maskedKey = secretStore.MaskApiKey(apiKey);
        AnsiConsole.MarkupLine($"[green]✓[/] API key saved for {provider}: {maskedKey}");
        logger.LogInformation("API key saved for {Provider}: {MaskedKey}", provider, maskedKey);
    }

    private static async Task ShowConfiguration(ILlmConfigurationService configService, ISecretStore secretStore,
        ILogger logger)
    {
        try
        {
            var config = await configService.LoadConfiguration();

            var table = new Table();
            table.AddColumn("Setting");
            table.AddColumn("Value");

            table.AddRow("Primary Provider", config.PrimaryProvider);
            table.AddRow("Primary Model", config.PrimaryModel);
            table.AddRow("Fallback Provider", config.FallbackProvider ?? "[grey](none)[/]");
            table.AddRow("Fallback Model", config.FallbackModel ?? "[grey](none)[/]");

            // Show API key status
            var primaryKey = await secretStore.GetApiKey(config.PrimaryProvider);
            var primaryKeyStatus = primaryKey != null
                ? $"[green]{secretStore.MaskApiKey(primaryKey)}[/]"
                : "[red](not set)[/]";
            table.AddRow($"{config.PrimaryProvider} API Key", primaryKeyStatus);

            if (config.FallbackProvider != null)
            {
                var fallbackKey = await secretStore.GetApiKey(config.FallbackProvider);
                var fallbackKeyStatus = fallbackKey != null
                    ? $"[green]{secretStore.MaskApiKey(fallbackKey)}[/]"
                    : "[red](not set)[/]";
                table.AddRow($"{config.FallbackProvider} API Key", fallbackKeyStatus);
            }

            AnsiConsole.Write(table);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error loading configuration:[/] {ex.Message}");
            logger.LogError(ex, "Failed to load configuration");
            Environment.Exit(1);
        }
    }
}

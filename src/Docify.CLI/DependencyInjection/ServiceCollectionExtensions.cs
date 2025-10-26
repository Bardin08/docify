using Docify.LLM.Abstractions;
using Docify.LLM.Configuration;
using Docify.LLM.ContextCollection;
using Docify.LLM.PromptEngineering;
using Docify.LLM.Providers;
using Docify.LLM.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Docify.CLI.DependencyInjection;

/// <summary>
/// Extension methods for configuring services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds LLM-related services to the service collection.
    /// </summary>
    public static IServiceCollection AddLlmServices(this IServiceCollection services)
    {
        // Context collection
        services.AddSingleton<ICallSiteCollector, CallSiteCollector>();
        services.AddSingleton<IContextCollector, SignatureContextCollector>();

        // Configuration
        services.AddSingleton<ILlmConfigurationService, LlmConfigurationService>();

        // Secret storage (platform-specific)
        services.AddSingleton<ISecretStore>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return SecretStoreFactory.GetPlatformSecretStore(loggerFactory);
        });

        // Prompt engineering
        services.AddSingleton<PromptBuilder>();

        // Provider factory
        services.AddSingleton<LlmProviderFactory>();

        return services;
    }
}

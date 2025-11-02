using Docify.Application.Generation.Interfaces;
using Docify.Application.Generation.Services;
using Docify.CLI.UserInteraction;
using Docify.Core.Caching;
using Docify.Core.Interfaces;
using Docify.LLM.Abstractions;
using Docify.LLM.Configuration;
using Docify.LLM.ContextCollection;
using Docify.LLM.PromptEngineering;
using Docify.LLM.Providers;
using Docify.LLM.Secrets;
using Docify.Writer.Backup;
using Docify.Writer.Interfaces;
using Docify.Writer.Writing;
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

    /// <summary>
    /// Adds Writer-related services to the service collection.
    /// </summary>
    public static IServiceCollection AddWriterServices(this IServiceCollection services)
    {
        services.AddSingleton<IBackupManager, BackupManager>();
        services.AddSingleton<IDocumentationWriter, DocumentationWriter>();

        return services;
    }

    /// <summary>
    /// Adds Core services including caching and generation orchestration.
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Caching
        services.AddSingleton<IDryRunCache, DryRunCacheManager>();

        // User interaction
        services.AddSingleton<IUserConfirmation, ConsoleUserConfirmation>();

        // Generation services
        services.AddSingleton<IPreviewGenerator, PreviewGenerator>();
        services.AddSingleton<IParallelDocumentationGenerator, ParallelDocumentationGenerator>();
        services.AddSingleton<IDocumentationOrchestrator, DocumentationOrchestrator>();

        return services;
    }
}

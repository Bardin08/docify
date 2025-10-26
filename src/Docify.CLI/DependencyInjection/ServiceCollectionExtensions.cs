using Docify.LLM.Abstractions;
using Docify.LLM.ContextCollection;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton<IContextCollector, SignatureContextCollector>();
        return services;
    }
}

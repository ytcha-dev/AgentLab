using AgentLab.Core.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AgentLab.Infrastructure.ModelProviders;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentModelProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<AgentModelsOptions>()
            .Bind(configuration.GetSection(AgentModelsOptions.SectionName))
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<AgentModelsOptions>,
                AgentModelsOptionsValidator>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IAgentModelProviderFactory,
                OllamaAgentModelProviderFactory>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IAgentModelProviderFactory,
                OpenAICompatibleAgentModelProviderFactory>());

        services.AddSingleton<IAgentModelResolver, ConfiguredAgentModelResolver>();

        return services;
    }
}

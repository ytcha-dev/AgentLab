using AgentLab.Core.Contracts;
using AgentLab.Core.Models;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;

namespace AgentLab.Infrastructure.ModelProviders;

public sealed class OllamaAgentModelProviderFactory : IAgentModelProviderFactory
{
    public string ProviderName => AgentModelProviderNames.Ollama;

    public IAgentModel Create(
        string profileName,
        AgentModelProfileOptions options)
    {
        var endpoint = new Uri(options.Endpoint!, UriKind.Absolute);
        var httpClient = new HttpClient
        {
            BaseAddress = endpoint,
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };

        IChatClient chatClient = new OllamaApiClient(
            httpClient,
            options.Model);

        return new ConfiguredAgentModel(
            new AgentModelDescriptor(
                profileName,
                ProviderName,
                options.Model),
            chatClient,
            (agentName, chatOptions) =>
            {
                if (options.ThinkByAgent.TryGetValue(agentName, out var agentThink))
                {
                    chatOptions.AddOllamaOption(OllamaOption.Think, agentThink);
                }
                else if (options.Think is not null)
                {
                    chatOptions.AddOllamaOption(OllamaOption.Think, options.Think.Value);
                }

                if (options.ContextWindow is not null)
                {
                    chatOptions.AddOllamaOption(
                        OllamaOption.NumCtx,
                        options.ContextWindow.Value);
                }
            },
            httpClient);
    }
}

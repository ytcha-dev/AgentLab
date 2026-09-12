using AgentLab.Core.Contracts;
using AgentLab.Core.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace AgentLab.Infrastructure.ModelProviders;

public sealed class OpenAICompatibleAgentModelProviderFactory : IAgentModelProviderFactory
{
    public string ProviderName => AgentModelProviderNames.OpenAICompatible;

    public IAgentModel Create(
        string profileName,
        AgentModelProfileOptions options)
    {
        var variableName = options.ApiKeyEnvironmentVariable
            ?? "OPENAI_API_KEY";
        var apiKey = Environment.GetEnvironmentVariable(variableName);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"Model profile '{profileName}' requires API key environment variable " +
                $"'{variableName}'.");
        }

        var clientOptions = new OpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };

        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            clientOptions.Endpoint = new Uri(options.Endpoint, UriKind.Absolute);
        }

        IChatClient chatClient = new OpenAI.Chat.ChatClient(
                options.Model,
                new ApiKeyCredential(apiKey),
                clientOptions)
            .AsIChatClient();

        return new ConfiguredAgentModel(
            new AgentModelDescriptor(
                profileName,
                ProviderName,
                options.Model),
            chatClient);
    }
}

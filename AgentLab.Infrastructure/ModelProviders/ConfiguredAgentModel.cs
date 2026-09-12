using AgentLab.Core.Contracts;
using AgentLab.Core.Models;
using Microsoft.Extensions.AI;

namespace AgentLab.Infrastructure.ModelProviders;

internal sealed class ConfiguredAgentModel(
    AgentModelDescriptor descriptor,
    IChatClient chatClient,
    Action<string, ChatOptions>? configureOptions = null,
    IDisposable? ownedResource = null) : IAgentModel, IDisposable
{
    private readonly Action<string, ChatOptions>? _configureOptions = configureOptions;
    private readonly IDisposable? _ownedResource = ownedResource;

    public AgentModelDescriptor Descriptor { get; } = descriptor;

    public IChatClient ChatClient { get; } = chatClient;

    public void ConfigureOptions(
        string agentName,
        ChatOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentNullException.ThrowIfNull(options);

        _configureOptions?.Invoke(agentName, options);
    }

    public void Dispose()
    {
        (ChatClient as IDisposable)?.Dispose();

        if (!ReferenceEquals(ChatClient, _ownedResource))
        {
            _ownedResource?.Dispose();
        }
    }
}

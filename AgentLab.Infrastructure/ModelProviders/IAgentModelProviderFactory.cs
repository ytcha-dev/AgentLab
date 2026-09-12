using AgentLab.Core.Contracts;

namespace AgentLab.Infrastructure.ModelProviders;

public interface IAgentModelProviderFactory
{
    string ProviderName { get; }

    IAgentModel Create(
        string profileName,
        AgentModelProfileOptions options);
}

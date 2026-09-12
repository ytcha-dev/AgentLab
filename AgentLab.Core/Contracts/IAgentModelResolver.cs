namespace AgentLab.Core.Contracts
{
    public interface IAgentModelResolver
    {
        IAgentModel Resolve(string agentName);
    }
}

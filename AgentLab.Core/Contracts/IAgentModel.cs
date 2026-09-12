namespace AgentLab.Core.Contracts
{
    public interface IAgentModel
    {
        AgentModelDescriptor Descriptor { get; }

        IChatClient ChatClient { get; }

        void ConfigureOptions(
            string agentName,
            ChatOptions options);
    }
}

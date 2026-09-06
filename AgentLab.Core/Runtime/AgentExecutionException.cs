namespace AgentLab.Core.Runtime
{
    public sealed class AgentExecutionException : Exception
    {
        public string AgentName { get; }

        public AgentFailureType FailureType { get; }

        public AgentExecutionException(
            string agentName,
            AgentFailureType failureType,
            string message,
            Exception? innerException = null)
            : base(message, innerException)
        {
            AgentName = agentName;
            FailureType = failureType;
        }
    }
}

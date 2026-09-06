namespace AgentLab.Core.Runtime
{
    public enum AgentFailureType
    {
        None,
        Timeout,
        Cancelled,
        OutputTruncated,
        EmptyResponse,
        InvalidStructuredOutput,
        ModelError
    }
}

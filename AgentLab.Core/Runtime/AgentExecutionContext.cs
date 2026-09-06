namespace AgentLab.Core.Runtime
{
    public sealed record AgentExecutionContext(
        string WorkflowId,
        int? Attempt = null);
}

namespace AgentLab.Core.Runtime
{
    public sealed record AgentExecutionContext(
        string RunId,
        string WorkflowId,
        string StepId,
        int Attempt);
}

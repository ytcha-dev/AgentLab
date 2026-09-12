namespace AgentLab.Core.Runtime;

public sealed record RunContext(
    string RunId,
    string WorkflowId)
{
    public static RunContext Create(string workflowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);

        return new RunContext(
            Guid.NewGuid().ToString("N"),
            workflowId);
    }

    public AgentExecutionContext ForStep(
        string stepId,
        int attempt = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);

        if (attempt < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attempt),
                "Attempt must be at least 1.");
        }

        return new AgentExecutionContext(
            RunId,
            WorkflowId,
            stepId,
            attempt);
    }
}

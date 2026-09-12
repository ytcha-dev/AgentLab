namespace AgentLab.Core.Models
{
    public sealed record WorkflowResult(
        RunContext Run,
        WorkflowStatus Status,
        Plan? Plan,
        Implementation? Implementation,
        ReviewResult? Review,
        IReadOnlyList<AgentExecutionResult> Steps,
        FailureInfo? Failure);
}

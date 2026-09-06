namespace AgentLab.Core.Models
{
    public sealed record WorkflowResult(
        Plan Plan,
        Implementation Implementation,
        ReviewResult Review);
}

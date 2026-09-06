namespace AgentLab.Core.Models
{
    public sealed record Plan(
        string Goal,
        IReadOnlyList<PlanStep> Steps);
}
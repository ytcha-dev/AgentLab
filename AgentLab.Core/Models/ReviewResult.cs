namespace AgentLab.Core.Models
{
    public sealed record ReviewResult(
        bool Approved,
        IReadOnlyList<ReviewIssue> Issues);
}

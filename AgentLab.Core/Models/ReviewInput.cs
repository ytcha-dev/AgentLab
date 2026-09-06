namespace AgentLab.Core.Models
{
    public sealed record ReviewInput(
        CodingTask Task,
        Plan Plan,
        Implementation Implementation);
}

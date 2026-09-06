namespace AgentLab.Core.Models
{
    public sealed record FixInput(
      CodingTask Task,
      Plan Plan,
      Implementation Implementation,
      ReviewResult Review);
}

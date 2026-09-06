namespace AgentLab.Core.Runtime
{
    public sealed record AgentExecutionResult(
        string AgentName,
        TimeSpan Duration,
        int? InputTokens,
        int? OutputTokens,
        string? FinishReason);
}

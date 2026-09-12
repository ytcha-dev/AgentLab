namespace AgentLab.Core.Runtime;

public sealed record UsageInfo(
    long? InputTokens,
    long? OutputTokens,
    long? TotalTokens)
{
    public static UsageInfo Unavailable { get; } = new(null, null, null);
}

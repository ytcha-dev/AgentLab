namespace AgentLab.Core.Runtime;

public sealed record FailureInfo(
    AgentFailureType Type,
    string Stage,
    string Message,
    bool Retryable,
    int Attempt,
    DateTimeOffset OccurredAt,
    string? ProviderCode = null,
    string? EvidenceRef = null);

namespace AgentLab.Core.Runtime
{
    public sealed record AgentExecutionResult(
        AgentExecutionContext Context,
        string AgentName,
        StepStatus Status,
        AgentModelDescriptor? Model,
        UsageInfo Usage,
        TimeSpan Duration,
        string? FinishReason,
        FailureInfo? Failure,
        [property: System.Text.Json.Serialization.JsonIgnore] string? Output)
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsSuccess => Status == StepStatus.Succeeded;
    }
}

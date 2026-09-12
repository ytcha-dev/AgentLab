using System.Text.Json.Serialization;

namespace AgentLab.Core.Runtime;

public sealed record AgentStepResult<T>(
    AgentExecutionResult Execution,
    T? Value)
{
    [JsonIgnore]
    public bool IsSuccess => Execution.IsSuccess && Value is not null;

    public static AgentStepResult<T> FromFailure(
        AgentExecutionResult execution)
    {
        ArgumentNullException.ThrowIfNull(execution);

        if (execution.IsSuccess)
        {
            throw new ArgumentException(
                "A successful execution cannot be converted to a failed step result.",
                nameof(execution));
        }

        return new AgentStepResult<T>(execution, default);
    }
}

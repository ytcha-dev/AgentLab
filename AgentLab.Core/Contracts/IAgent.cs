namespace AgentLab.Core.Contracts
{
    public interface IAgent<in TInput, TOutput>
    {
        Task<AgentStepResult<TOutput>> ExecuteAsync(
            TInput input,
            AgentExecutionContext context,
            CancellationToken cancellationToken = default);
    }
}

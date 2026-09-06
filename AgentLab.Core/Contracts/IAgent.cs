namespace AgentLab.Core.Contracts
{
    public interface IAgent<in TInput, TOutput>
    {
        Task<TOutput> ExecuteAsync(
            TInput input,
            CancellationToken cancellationToken = default);
    }
}

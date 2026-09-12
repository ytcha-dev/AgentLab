using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AgentLab.Core.Runtime
{
    public class AgentExecutor(
        IAgentModelResolver modelResolver,
        ILogger<AgentExecutor> logger)
    {
        private readonly IAgentModelResolver _modelResolver = modelResolver;
        private readonly ILogger<AgentExecutor> _logger = logger;

        public async Task<ChatResponse> ExecuteAsync(
            string agentName,
            IReadOnlyList<ChatMessage> messages,
            ChatOptions options,
            CancellationToken cancellationToken = default)
        {
            var agentModel = _modelResolver.Resolve(agentName);
            agentModel.ConfigureOptions(agentName, options);

            var executionId = Guid.NewGuid()
                .ToString("N")[..8];

            var sw = Stopwatch.StartNew();

            _logger.LogInformation(
                "[{AgentName}][{ExecutionId}] START Profile={ProfileName} Provider={Provider} Model={Model} MaxOutputTokens={MaxOutputTokens}",
                agentName,
                executionId,
                agentModel.Descriptor.ProfileName,
                agentModel.Descriptor.Provider,
                agentModel.Descriptor.Model,
                options.MaxOutputTokens);

            try
            {
                var response = await agentModel.ChatClient.GetResponseAsync(
                    messages,
                    options,
                    cancellationToken);

                sw.Stop();

                var finishReason = response.FinishReason?.ToString();

                if (finishReason?.Equals(
                        "length",
                        StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.LogWarning(
                        "[{AgentName}][{ExecutionId}] TRUNCATED Duration={Duration:F2}s InputTokens={InputTokens} OutputTokens={OutputTokens}",
                        agentName,
                        executionId,
                        sw.Elapsed.TotalSeconds,
                        response.Usage?.InputTokenCount,
                        response.Usage?.OutputTokenCount);

                    throw new AgentExecutionException(
                        agentName,
                        AgentFailureType.OutputTruncated,
                        $"{agentName} output was truncated.");
                }

                _logger.LogInformation(
                    "[{AgentName}][{ExecutionId}] END Duration={Duration:F2}s InputTokens={InputTokens} OutputTokens={OutputTokens} FinishReason={FinishReason}",
                    agentName,
                    executionId,
                    sw.Elapsed.TotalSeconds,
                    response.Usage?.InputTokenCount,
                    response.Usage?.OutputTokenCount,
                    response.FinishReason);

                return response;
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

                _logger.LogWarning(
                    "[{AgentName}][{ExecutionId}] CANCELLED Duration={Duration:F2}s",
                    agentName,
                    executionId,
                    sw.Elapsed.TotalSeconds);

                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();

                _logger.LogError(
                    ex,
                    "[{AgentName}][{ExecutionId}] ERROR Duration={Duration:F2}s",
                    agentName,
                    executionId,
                    sw.Elapsed.TotalSeconds);

                throw;
            }
        }
    }
}

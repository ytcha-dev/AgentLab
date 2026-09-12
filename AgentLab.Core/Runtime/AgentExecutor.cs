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

        public async Task<AgentExecutionResult> ExecuteAsync(
            AgentExecutionContext context,
            string agentName,
            IReadOnlyList<ChatMessage> messages,
            ChatOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
            ArgumentNullException.ThrowIfNull(messages);
            ArgumentNullException.ThrowIfNull(options);

            var sw = Stopwatch.StartNew();
            IAgentModel? agentModel = null;

            try
            {
                agentModel = _modelResolver.Resolve(agentName);
                agentModel.ConfigureOptions(agentName, options);

                _logger.LogInformation(
                    "[{RunId}][{StepId}][{AgentName}][Attempt={Attempt}] START Workflow={WorkflowId} Profile={ProfileName} Provider={Provider} Model={Model} MaxOutputTokens={MaxOutputTokens}",
                    context.RunId,
                    context.StepId,
                    agentName,
                    context.Attempt,
                    context.WorkflowId,
                    agentModel.Descriptor.ProfileName,
                    agentModel.Descriptor.Provider,
                    agentModel.Descriptor.Model,
                    options.MaxOutputTokens);

                var response = await agentModel.ChatClient.GetResponseAsync(
                    messages,
                    options,
                    cancellationToken);

                sw.Stop();

                var finishReason = response.FinishReason?.ToString();
                var usage = ToUsageInfo(response.Usage);

                if (finishReason?.Equals(
                        "length",
                        StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.LogWarning(
                        "[{RunId}][{StepId}][{AgentName}][Attempt={Attempt}] TRUNCATED Duration={Duration:F2}s InputTokens={InputTokens} OutputTokens={OutputTokens}",
                        context.RunId,
                        context.StepId,
                        agentName,
                        context.Attempt,
                        sw.Elapsed.TotalSeconds,
                        usage.InputTokens,
                        usage.OutputTokens);

                    return CreateFailureResult(
                        context,
                        agentName,
                        StepStatus.Failed,
                        agentModel.Descriptor,
                        usage,
                        sw.Elapsed,
                        finishReason,
                        AgentFailureType.OutputTruncated,
                        $"{agentName} output was truncated.");
                }

                _logger.LogInformation(
                    "[{RunId}][{StepId}][{AgentName}][Attempt={Attempt}] END Duration={Duration:F2}s InputTokens={InputTokens} OutputTokens={OutputTokens} FinishReason={FinishReason}",
                    context.RunId,
                    context.StepId,
                    agentName,
                    context.Attempt,
                    sw.Elapsed.TotalSeconds,
                    usage.InputTokens,
                    usage.OutputTokens,
                    response.FinishReason);

                return new AgentExecutionResult(
                    context,
                    agentName,
                    StepStatus.Succeeded,
                    agentModel.Descriptor,
                    usage,
                    sw.Elapsed,
                    finishReason,
                    null,
                    response.Text);
            }
            catch (OperationCanceledException ex)
            {
                sw.Stop();

                _logger.LogWarning(
                    "[{RunId}][{StepId}][{AgentName}][Attempt={Attempt}] CANCELLED Duration={Duration:F2}s",
                    context.RunId,
                    context.StepId,
                    agentName,
                    context.Attempt,
                    sw.Elapsed.TotalSeconds);

                return CreateFailureResult(
                    context,
                    agentName,
                    StepStatus.Cancelled,
                    agentModel?.Descriptor,
                    UsageInfo.Unavailable,
                    sw.Elapsed,
                    null,
                    AgentFailureType.Cancelled,
                    ex.Message);
            }
            catch (Exception ex)
            {
                sw.Stop();

                _logger.LogError(
                    ex,
                    "[{RunId}][{StepId}][{AgentName}][Attempt={Attempt}] ERROR Duration={Duration:F2}s",
                    context.RunId,
                    context.StepId,
                    agentName,
                    context.Attempt,
                    sw.Elapsed.TotalSeconds);

                return CreateFailureResult(
                    context,
                    agentName,
                    StepStatus.Failed,
                    agentModel?.Descriptor,
                    UsageInfo.Unavailable,
                    sw.Elapsed,
                    null,
                    ex is AgentExecutionException agentException
                        ? agentException.FailureType
                        : AgentFailureType.ModelError,
                    ex.Message);
            }
        }

        private static UsageInfo ToUsageInfo(UsageDetails? usage) =>
            usage is null
                ? UsageInfo.Unavailable
                : new UsageInfo(
                    usage.InputTokenCount,
                    usage.OutputTokenCount,
                    usage.TotalTokenCount);

        private static AgentExecutionResult CreateFailureResult(
            AgentExecutionContext context,
            string agentName,
            StepStatus status,
            AgentModelDescriptor? model,
            UsageInfo usage,
            TimeSpan duration,
            string? finishReason,
            AgentFailureType failureType,
            string message) =>
            new(
                context,
                agentName,
                status,
                model,
                usage,
                duration,
                finishReason,
                new FailureInfo(
                    failureType,
                    context.StepId,
                    message,
                    false,
                    context.Attempt,
                    DateTimeOffset.UtcNow),
                null);
    }
}

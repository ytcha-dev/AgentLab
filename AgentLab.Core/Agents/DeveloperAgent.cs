namespace AgentLab.Core.Agents
{
    public class DeveloperAgent(AgentExecutor executor) : IAgent<DeveloperInput, Implementation>
    {
        private readonly AgentExecutor _executor = executor;

        public async Task<AgentStepResult<Implementation>> ExecuteAsync(
            DeveloperInput input,
            AgentExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var messages = new ChatMessage[]
            {
                new(
                ChatRole.System,
                    """
                    You are a senior .NET implementation agent.

                    Implement the requested coding task based on the provided plan.

                    Rules:
                    - Implement only what is required by the original goal and plan.
                    - Do not introduce additional frameworks unless explicitly required.
                    - Prefer built-in ASP.NET Core features over third-party libraries.
                    - Do not add authentication, anti-forgery, global exception handling, or extra service layers unless required.
                    - Keep the implementation minimal.
                    - All code must be compilable.
                    """),

                new(
                    ChatRole.User,
                    $"""
                    Original goal:
                    {input.Task.Goal}

                    Implementation plan:
                        {string.Join(
                            Environment.NewLine,
                            input.Plan.Steps.Select(
                                x => $"{x.Id}. {x.Description}"))}
                    """)
            };

            var options = new ChatOptions
            {
                MaxOutputTokens = 6000,
            };

            var execution = await _executor.ExecuteAsync(
                context,
                "developer",
                messages,
                options,
                cancellationToken);

            if (!execution.IsSuccess)
            {
                return AgentStepResult<Implementation>.FromFailure(execution);
            }

            if (string.IsNullOrWhiteSpace(execution.Output))
            {
                throw new InvalidOperationException(
                    $"Developer returned no final text. " +
                    $"FinishReason: {execution.FinishReason}");
            }

            return new AgentStepResult<Implementation>(
                execution,
                new Implementation(execution.Output));
        }
    }
}

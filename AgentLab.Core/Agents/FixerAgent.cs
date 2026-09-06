namespace AgentLab.Core.Agents
{
    public class FixerAgent(AgentExecutor executor) : IAgent<FixInput, Implementation>
    {
        private readonly AgentExecutor _executor = executor;

        public async Task<Implementation> ExecuteAsync(FixInput input, CancellationToken cancellationToken = default)
        {
            var messages = new List<ChatMessage>
        {
            new(
                ChatRole.System,
                """
                You are a senior .NET fixing agent.

                Your job is to correct the implementation using the review issues.

                Rules:
                - Preserve correct parts of the implementation.
                - Fix compilation errors first.
                - Remove unnecessary dependencies and complexity.
                - Do not introduce new frameworks unless required.
                - If a review issue is technically incorrect, do not blindly follow it.
                - Prefer idiomatic ASP.NET Core and EF Core.
                - Return the complete corrected implementation.
                """),

            new(
                ChatRole.User,
                $"""
                Original goal:
                {input.Task.Goal}

                Plan:
                {string.Join(
                    Environment.NewLine,
                    input.Plan.Steps.Select(
                        x => $"{x.Id}. {x.Description}"))}

                Current implementation:
                {input.Implementation.Content}

                Review issues:
                {string.Join(
                    Environment.NewLine,
                    input.Review.Issues.Select(
                        x => $"[{x.Severity}] {x.Description}"))}
                """)
        };

            var options = new ChatOptions
            {
                MaxOutputTokens = 6000
            };

            options.AddOllamaOption(
                OllamaOption.Think,
                true);

            options.AddOllamaOption(
                OllamaOption.NumCtx,
                8192);

            var response = await _executor.ExecuteAsync(
                "fixer",
                messages,
                options,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw new InvalidOperationException(
                    $"Fixer returned no final text. " +
                    $"FinishReason: {response.FinishReason}");
            }

            return new Implementation(response.Text);
        }
    }
}

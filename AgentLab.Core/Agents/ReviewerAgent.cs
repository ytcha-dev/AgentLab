namespace AgentLab.Core.Agents
{
    public class ReviewerAgent(AgentExecutor executor) : IAgent<ReviewInput, ReviewResult>
    {
        private readonly AgentExecutor _executor = executor;

        public async Task<AgentStepResult<ReviewResult>> ExecuteAsync(
            ReviewInput input,
            AgentExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var messages = new List<ChatMessage>
        {
            new(
                ChatRole.System,
                """
                You are a strict senior .NET code reviewer.

                Evaluate whether the implementation satisfies the original goal
                and whether the proposed code is technically plausible.

                Review for:
                - compilation errors
                - invalid or suspicious APIs
                - missing requirements
                - unnecessary dependencies
                - unnecessary complexity
                - contradictions between implementation choices
                - ASP.NET Core and EF Core misuse
                - validation mistakes

                Rules:
                - Do not rewrite the implementation.
                - Do not propose full replacement code.
                - Return JSON only.
                - Approve only if there are no meaningful correctness,
                  requirement, or design issues.
                - Severity must be one of: low, medium, high.
                """),

            new(
                ChatRole.User,
                $$$"""
                Original goal:
                {{{input.Task.Goal}}}

                Plan:
                {{{string.Join(
                    Environment.NewLine,
                    input.Plan.Steps.Select(
                        x => $"{x.Id}. {x.Description}"))}}}

                Implementation:
                {{{input.Implementation.Content}}}

                Return JSON using exactly this structure:

                {{
                  "approved": false,
                  "issues": [
                    {{
                      "severity": "high",
                      "description": "string"
                    }}
                  ]
                }}
                """)
        };

            var options = new ChatOptions
            {
                MaxOutputTokens = 2000,
                Temperature = 0.1f
            };

            var execution = await _executor.ExecuteAsync(
                context,
                "reviewer",
                messages,
                options,
                cancellationToken);

            if (!execution.IsSuccess)
            {
                return AgentStepResult<ReviewResult>.FromFailure(execution);
            }

            if (string.IsNullOrWhiteSpace(execution.Output))
            {
                throw new InvalidOperationException(
                    $"Reviewer returned no final text. " +
                    $"FinishReason: {execution.FinishReason}");
            }

            var result = JsonSerializer.Deserialize<ReviewResult>(
                execution.Output,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return new AgentStepResult<ReviewResult>(
                execution,
                result
                    ?? throw new InvalidOperationException(
                        "Reviewer response could not be deserialized."));
        }
    }
}

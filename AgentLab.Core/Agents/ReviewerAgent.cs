namespace AgentLab.Core.Agents
{
    public class ReviewerAgent(AgentExecutor executor) : IAgent<ReviewInput, ReviewResult>
    {
        private readonly AgentExecutor _executor = executor;

        public async Task<ReviewResult> ExecuteAsync(
            ReviewInput input,
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

            options.AddOllamaOption(
                OllamaOption.Think,
                false);


            options.AddOllamaOption(
                OllamaOption.NumCtx,
                8192);

            var response = await _executor.ExecuteAsync(
                "reviewer",
                messages,
                options,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw new InvalidOperationException(
                    $"Reviewer returned no final text. " +
                    $"FinishReason: {response.FinishReason}");
            }

            var result = JsonSerializer.Deserialize<ReviewResult>(
                response.Text,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return result
                ?? throw new InvalidOperationException(
                    "Reviewer response could not be deserialized.");
        }
    }
}

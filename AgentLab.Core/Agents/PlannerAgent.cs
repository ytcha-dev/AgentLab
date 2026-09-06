namespace AgentLab.Core.Agents
{
    public class PlannerAgent(AgentExecutor executor) : IAgent<CodingTask, Plan>
    {
        private readonly AgentExecutor _executor = executor;

        public static JsonSerializerOptions SerializerOptions => new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<Plan> ExecuteAsync(
            CodingTask input,
            CancellationToken cancellationToken = default)
        {
            var messages = new ChatMessage[]
            {
                new (
                    ChatRole.System,
                    """
                    You are a software planning agent.

                    Convert the user's coding goal into a concise implementation plan.

                    Rules:
                    - Do not write implementation code.
                    - Produce 3 to 6 actionable steps.
                    - Each step must be independently understandable.
                    - Prefer the simplest design that satisfies the goal.
                    - Return JSON only.
                    - Do not wrap the JSON in markdown.
                    """),

                new(
                    ChatRole.User,
                    $$$"""
                    Goal:
                    {{{input.Goal}}}

                    Return JSON using this exact structure:

                    {{
                      "goal": "string",
                      "steps": [
                        {{
                          "id": 1,
                          "description": "string"
                        }}
                      ]
                    }}
                    """)
            };

            var options = new ChatOptions
            {
                MaxOutputTokens = 700,
                //Temperature = 0.2f
            };

            options.AddOllamaOption(
                OllamaOption.Think,
                false);

            options.AddOllamaOption(
                OllamaOption.NumCtx,
                8192);

            var response = await _executor.ExecuteAsync(
                "planner",
                messages,
                options,
                cancellationToken);

            var json = response.Text
                ?? throw new InvalidOperationException(
                    "Planner agent did not return a response.");

            var plan = JsonSerializer.Deserialize<Plan>(
                json,
                SerializerOptions);

            return plan
                ?? throw new InvalidOperationException(
                    "Planner agent returned invalid JSON.");
        }
    }
}

namespace AgentLab.Core.Agents
{
    public class DeveloperAgent(IChatClient chatClient) : IAgent<DeveloperInput, Implementation>
    {
        private readonly IChatClient _chatClient = chatClient;

        public async Task<Implementation> ExecuteAsync(
            DeveloperInput input,
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
                MaxOutputTokens = 25000,
            };

            options.AddOllamaOption(
                OllamaOption.Think,
                true);

            var response = await _chatClient.GetResponseAsync(
                messages,
                options,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw new InvalidOperationException(
                    $"Developer returned no final text. " +
                    $"FinishReason: {response.FinishReason}");
            }

            return new Implementation(response.Text);
        }
    }
}

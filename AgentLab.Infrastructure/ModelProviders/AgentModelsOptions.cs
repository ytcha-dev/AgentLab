namespace AgentLab.Infrastructure.ModelProviders;

public sealed class AgentModelsOptions
{
    public const string SectionName = "AgentModels";

    public string DefaultProfile { get; set; } = string.Empty;

    public Dictionary<string, string> Agents { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, AgentModelProfileOptions> Profiles { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AgentModelProfileOptions
{
    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? Endpoint { get; set; }

    public string? ApiKeyEnvironmentVariable { get; set; }

    public int TimeoutSeconds { get; set; } = 300;

    public int? ContextWindow { get; set; }

    public bool? Think { get; set; }

    public Dictionary<string, bool> ThinkByAgent { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

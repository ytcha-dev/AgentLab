using Microsoft.Extensions.Options;

namespace AgentLab.Infrastructure.ModelProviders;

public sealed class AgentModelsOptionsValidator : IValidateOptions<AgentModelsOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        AgentModelsOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DefaultProfile))
        {
            failures.Add("AgentModels:DefaultProfile is required.");
        }

        if (options.Profiles.Count == 0)
        {
            failures.Add("AgentModels:Profiles must contain at least one profile.");
        }

        var profiles = new Dictionary<string, AgentModelProfileOptions>(
            options.Profiles,
            StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(options.DefaultProfile) &&
            !profiles.ContainsKey(options.DefaultProfile))
        {
            failures.Add(
                $"AgentModels:DefaultProfile references unknown profile '{options.DefaultProfile}'.");
        }

        foreach (var (profileName, profile) in profiles)
        {
            var path = $"AgentModels:Profiles:{profileName}";

            if (string.IsNullOrWhiteSpace(profileName))
            {
                failures.Add("AgentModels contains an empty profile name.");
            }

            if (string.IsNullOrWhiteSpace(profile.Provider))
            {
                failures.Add($"{path}:Provider is required.");
            }

            if (string.IsNullOrWhiteSpace(profile.Model))
            {
                failures.Add($"{path}:Model is required.");
            }

            if (profile.TimeoutSeconds is < 1 or > 1800)
            {
                failures.Add($"{path}:TimeoutSeconds must be between 1 and 1800.");
            }

            if (profile.ContextWindow is <= 0)
            {
                failures.Add($"{path}:ContextWindow must be greater than zero.");
            }

            if (!string.IsNullOrWhiteSpace(profile.Endpoint) &&
                (!Uri.TryCreate(profile.Endpoint, UriKind.Absolute, out var endpoint) ||
                 endpoint.Scheme is not ("http" or "https")))
            {
                failures.Add($"{path}:Endpoint must be an absolute HTTP or HTTPS URI.");
            }

            if (profile.Provider.Equals(
                    AgentModelProviderNames.Ollama,
                    StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(profile.Endpoint))
            {
                failures.Add($"{path}:Endpoint is required for Ollama.");
            }

            if (profile.ApiKeyEnvironmentVariable is not null &&
                string.IsNullOrWhiteSpace(profile.ApiKeyEnvironmentVariable))
            {
                failures.Add($"{path}:ApiKeyEnvironmentVariable cannot be empty.");
            }
        }

        foreach (var (agentName, profileName) in options.Agents)
        {
            if (string.IsNullOrWhiteSpace(agentName))
            {
                failures.Add("AgentModels:Agents contains an empty agent name.");
            }

            if (!profiles.ContainsKey(profileName))
            {
                failures.Add(
                    $"AgentModels:Agents:{agentName} references unknown profile '{profileName}'.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

using System.Collections.Concurrent;
using AgentLab.Core.Contracts;
using Microsoft.Extensions.Options;

namespace AgentLab.Infrastructure.ModelProviders;

public sealed class ConfiguredAgentModelResolver : IAgentModelResolver, IDisposable
{
    private readonly AgentModelsOptions _options;
    private readonly IReadOnlyDictionary<string, AgentModelProfileOptions> _profiles;
    private readonly IReadOnlyDictionary<string, string> _agents;
    private readonly IReadOnlyDictionary<string, IAgentModelProviderFactory> _factories;
    private readonly ConcurrentDictionary<string, Lazy<IAgentModel>> _models =
        new(StringComparer.OrdinalIgnoreCase);

    public ConfiguredAgentModelResolver(
        IOptions<AgentModelsOptions> options,
        IEnumerable<IAgentModelProviderFactory> factories)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(factories);

        _options = options.Value;
        _profiles = new Dictionary<string, AgentModelProfileOptions>(
            _options.Profiles,
            StringComparer.OrdinalIgnoreCase);
        _agents = new Dictionary<string, string>(
            _options.Agents,
            StringComparer.OrdinalIgnoreCase);
        _factories = factories.ToDictionary(
            factory => factory.ProviderName,
            StringComparer.OrdinalIgnoreCase);
    }

    public IAgentModel Resolve(string agentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        var profileName = _agents.TryGetValue(agentName, out var agentProfile)
            ? agentProfile
            : _options.DefaultProfile;

        if (!_profiles.TryGetValue(profileName, out var profile))
        {
            throw new InvalidOperationException(
                $"Agent model profile '{profileName}' was not found for agent '{agentName}'.");
        }

        return _models.GetOrAdd(
            profileName,
            _ => new Lazy<IAgentModel>(
                () => CreateModel(profileName, profile),
                LazyThreadSafetyMode.ExecutionAndPublication))
            .Value;
    }

    private IAgentModel CreateModel(
        string profileName,
        AgentModelProfileOptions profile)
    {
        if (!_factories.TryGetValue(profile.Provider, out var factory))
        {
            var registeredProviders = string.Join(
                ", ",
                _factories.Keys.Order(StringComparer.OrdinalIgnoreCase));

            throw new InvalidOperationException(
                $"Provider '{profile.Provider}' for profile '{profileName}' is not registered. " +
                $"Registered providers: {registeredProviders}.");
        }

        return factory.Create(profileName, profile);
    }

    public void Dispose()
    {
        foreach (var model in _models.Values
                     .Where(value => value.IsValueCreated)
                     .Select(value => value.Value)
                     .Distinct())
        {
            (model as IDisposable)?.Dispose();
        }
    }
}

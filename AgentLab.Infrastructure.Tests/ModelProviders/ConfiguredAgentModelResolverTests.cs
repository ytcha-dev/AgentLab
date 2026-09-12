using AgentLab.Core.Contracts;
using AgentLab.Core.Models;
using AgentLab.Infrastructure.ModelProviders;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentLab.Infrastructure.Tests.ModelProviders;

public sealed class ConfiguredAgentModelResolverTests
{
    [Fact]
    public void Resolve_UsesDefaultProfileForUnmappedAgent()
    {
        var localFactory = new FakeProviderFactory("LocalProvider");
        using var resolver = CreateResolver(localFactory);

        var model = resolver.Resolve("developer");

        Assert.Equal("local", model.Descriptor.ProfileName);
        Assert.Equal("local-model", model.Descriptor.Model);
    }

    [Fact]
    public void Resolve_UsesAgentOverride()
    {
        var localFactory = new FakeProviderFactory("LocalProvider");
        var cloudFactory = new FakeProviderFactory("CloudProvider");
        using var resolver = CreateResolver(localFactory, cloudFactory);

        var model = resolver.Resolve("reviewer");

        Assert.Equal("cloud", model.Descriptor.ProfileName);
        Assert.Equal("cloud-model", model.Descriptor.Model);
    }

    [Fact]
    public void Resolve_CachesOneModelPerProfile()
    {
        var localFactory = new FakeProviderFactory("LocalProvider");
        using var resolver = CreateResolver(localFactory);

        var first = resolver.Resolve("planner");
        var second = resolver.Resolve("developer");

        Assert.Same(first, second);
        Assert.Equal(1, localFactory.CreateCount);
    }

    [Fact]
    public void Resolve_ReportsUnregisteredProvider()
    {
        using var resolver = CreateResolver();

        var exception = Assert.Throws<InvalidOperationException>(
            () => resolver.Resolve("developer"));

        Assert.Contains("LocalProvider", exception.Message);
        Assert.Contains("not registered", exception.Message);
    }

    private static ConfiguredAgentModelResolver CreateResolver(
        params IAgentModelProviderFactory[] factories)
    {
        var options = new AgentModelsOptions
        {
            DefaultProfile = "local",
            Agents =
            {
                ["reviewer"] = "cloud"
            },
            Profiles =
            {
                ["local"] = new AgentModelProfileOptions
                {
                    Provider = "LocalProvider",
                    Model = "local-model"
                },
                ["cloud"] = new AgentModelProfileOptions
                {
                    Provider = "CloudProvider",
                    Model = "cloud-model"
                }
            }
        };

        return new ConfiguredAgentModelResolver(
            Options.Create(options),
            factories);
    }

    private sealed class FakeProviderFactory(string providerName)
        : IAgentModelProviderFactory
    {
        public int CreateCount { get; private set; }

        public string ProviderName { get; } = providerName;

        public IAgentModel Create(
            string profileName,
            AgentModelProfileOptions options)
        {
            CreateCount++;

            return new FakeAgentModel(
                new AgentModelDescriptor(
                    profileName,
                    ProviderName,
                    options.Model));
        }
    }

    private sealed class FakeAgentModel(AgentModelDescriptor descriptor)
        : IAgentModel
    {
        public AgentModelDescriptor Descriptor { get; } = descriptor;

        public IChatClient ChatClient => null!;

        public void ConfigureOptions(
            string agentName,
            ChatOptions options)
        {
        }
    }
}

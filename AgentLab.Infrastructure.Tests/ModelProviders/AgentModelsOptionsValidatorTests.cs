using AgentLab.Infrastructure.ModelProviders;
using Xunit;

namespace AgentLab.Infrastructure.Tests.ModelProviders;

public sealed class AgentModelsOptionsValidatorTests
{
    private readonly AgentModelsOptionsValidator _validator = new();

    [Fact]
    public void Validate_AcceptsValidConfiguration()
    {
        var options = CreateValidOptions();

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_RejectsUnknownDefaultProfile()
    {
        var options = CreateValidOptions();
        options.DefaultProfile = "missing";

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("unknown profile 'missing'", result.FailureMessage);
    }

    [Fact]
    public void Validate_RejectsUnknownAgentProfile()
    {
        var options = CreateValidOptions();
        options.Agents["reviewer"] = "missing";

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Agents:reviewer", result.FailureMessage);
    }

    [Fact]
    public void Validate_RequiresOllamaEndpoint()
    {
        var options = CreateValidOptions();
        options.Profiles["local"].Endpoint = null;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Endpoint is required for Ollama", result.FailureMessage);
    }

    private static AgentModelsOptions CreateValidOptions()
    {
        return new AgentModelsOptions
        {
            DefaultProfile = "local",
            Profiles =
            {
                ["local"] = new AgentModelProfileOptions
                {
                    Provider = AgentModelProviderNames.Ollama,
                    Model = "test-model",
                    Endpoint = "http://localhost:11434"
                }
            }
        };
    }
}

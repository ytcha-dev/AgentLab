using AgentLab.Infrastructure.ModelProviders;
using Xunit;

namespace AgentLab.Infrastructure.Tests.ModelProviders;

public sealed class OpenAICompatibleAgentModelProviderFactoryTests
{
    [Fact]
    public void Create_BuildsClientWithoutCallingProvider()
    {
        var variableName = $"AGENTLAB_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(variableName, "test-key");

        try
        {
            var factory = new OpenAICompatibleAgentModelProviderFactory();
            var options = new AgentModelProfileOptions
            {
                Provider = AgentModelProviderNames.OpenAICompatible,
                Model = "test-model",
                Endpoint = "http://localhost:1/v1",
                ApiKeyEnvironmentVariable = variableName
            };

            var model = factory.Create("cloud", options);

            Assert.Equal("cloud", model.Descriptor.ProfileName);
            Assert.Equal(AgentModelProviderNames.OpenAICompatible, model.Descriptor.Provider);
            Assert.NotNull(model.ChatClient);
            (model as IDisposable)?.Dispose();
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    public void Create_ReportsMissingApiKeyWithoutCallingProvider()
    {
        var variableName = $"AGENTLAB_TEST_{Guid.NewGuid():N}";
        var factory = new OpenAICompatibleAgentModelProviderFactory();
        var options = new AgentModelProfileOptions
        {
            Provider = AgentModelProviderNames.OpenAICompatible,
            Model = "test-model",
            ApiKeyEnvironmentVariable = variableName
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.Create("cloud", options));

        Assert.Contains("cloud", exception.Message);
        Assert.Contains(variableName, exception.Message);
    }
}

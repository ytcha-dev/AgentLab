using AgentLab.Infrastructure.ModelProviders;
using Xunit;

namespace AgentLab.Infrastructure.Tests.ModelProviders;

public sealed class OllamaAgentModelProviderFactoryTests
{
    [Fact]
    public void Create_BuildsClientWithoutCallingProvider()
    {
        var factory = new OllamaAgentModelProviderFactory();
        var options = new AgentModelProfileOptions
        {
            Provider = AgentModelProviderNames.Ollama,
            Model = "test-model",
            Endpoint = "http://localhost:11434",
            ContextWindow = 8192,
            ThinkByAgent =
            {
                ["developer"] = true
            }
        };

        var model = factory.Create("local", options);

        Assert.Equal("local", model.Descriptor.ProfileName);
        Assert.Equal(AgentModelProviderNames.Ollama, model.Descriptor.Provider);
        Assert.NotNull(model.ChatClient);
        (model as IDisposable)?.Dispose();
    }
}

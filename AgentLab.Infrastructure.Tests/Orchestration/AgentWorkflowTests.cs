using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentLab.Core.Agents;
using AgentLab.Core.Contracts;
using AgentLab.Core.Models;
using AgentLab.Core.Orchestration;
using AgentLab.Core.Runtime;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgentLab.Infrastructure.Tests.Orchestration;

public sealed class AgentWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_WhenReviewerApproves_ReturnsSucceededWithCorrelatedSteps()
    {
        var workflow = CreateWorkflow(
            PlannerResponse,
            "implementation-v1",
            ApprovedReview);
        var run = new RunContext("run-success", "software-development");

        var result = await workflow.ExecuteAsync(
            new CodingTask("Build a todo API."),
            run);

        Assert.Equal(WorkflowStatus.Succeeded, result.Status);
        Assert.Equal(run, result.Run);
        Assert.NotNull(result.Plan);
        Assert.NotNull(result.Implementation);
        Assert.True(result.Review?.Approved);
        Assert.Null(result.Failure);
        Assert.Collection(
            result.Steps,
            step => AssertStep(step, run, "planner", "plan", 1),
            step => AssertStep(step, run, "developer", "develop", 1),
            step => AssertStep(step, run, "reviewer", "review", 1));

        using var serializedStep = JsonDocument.Parse(
            JsonSerializer.Serialize(result.Steps[0]));
        Assert.False(
            serializedStep.RootElement.TryGetProperty(
                nameof(AgentExecutionResult.Output),
                out _));
    }

    [Fact]
    public async Task ExecuteAsync_WhenFirstReviewRejects_RunsFixerAndCanSucceed()
    {
        var workflow = CreateWorkflow(
            PlannerResponse,
            "implementation-v1",
            RejectedReview,
            "implementation-v2",
            ApprovedReview);
        var run = new RunContext("run-fixed", "software-development");

        var result = await workflow.ExecuteAsync(
            new CodingTask("Build a todo API."),
            run);

        Assert.Equal(WorkflowStatus.Succeeded, result.Status);
        Assert.Equal("implementation-v2", result.Implementation?.Content);
        Assert.Collection(
            result.Steps,
            step => AssertStep(step, run, "planner", "plan", 1),
            step => AssertStep(step, run, "developer", "develop", 1),
            step => AssertStep(step, run, "reviewer", "review", 1),
            step => AssertStep(step, run, "fixer", "fix", 1),
            step => AssertStep(step, run, "reviewer", "review", 2));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllReviewsReject_ReturnsNotApproved()
    {
        var workflow = CreateWorkflow(
            PlannerResponse,
            "implementation-v1",
            RejectedReview,
            "implementation-v2",
            RejectedReview,
            "implementation-v3",
            RejectedReview);
        var run = new RunContext("run-rejected", "software-development");

        var result = await workflow.ExecuteAsync(
            new CodingTask("Build a todo API."),
            run);

        Assert.Equal(WorkflowStatus.NotApproved, result.Status);
        Assert.False(result.Review?.Approved);
        Assert.Null(result.Failure);
        Assert.Equal(7, result.Steps.Count);
        Assert.Equal(3, result.Steps.Count(step => step.AgentName == "reviewer"));
        Assert.Equal(2, result.Steps.Count(step => step.AgentName == "fixer"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderFails_ReturnsFailedResult()
    {
        var workflow = CreateWorkflow(
            new InvalidOperationException("provider unavailable"));

        var result = await workflow.ExecuteAsync(
            new CodingTask("Build a todo API."),
            new RunContext("run-failed", "software-development"));

        Assert.Equal(WorkflowStatus.Failed, result.Status);
        Assert.Equal(AgentFailureType.ModelError, result.Failure?.Type);
        Assert.Single(result.Steps);
        Assert.Equal(StepStatus.Failed, result.Steps[0].Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenModelCallIsCancelled_ReturnsCancelledResult()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var workflow = CreateWorkflow(new OperationCanceledException(cancellation.Token));

        var result = await workflow.ExecuteAsync(
            new CodingTask("Build a todo API."),
            new RunContext("run-cancelled", "software-development"),
            cancellation.Token);

        Assert.Equal(WorkflowStatus.Cancelled, result.Status);
        Assert.Equal(AgentFailureType.Cancelled, result.Failure?.Type);
        Assert.Single(result.Steps);
        Assert.Equal(StepStatus.Cancelled, result.Steps[0].Status);
    }

    private static AgentWorkflow CreateWorkflow(params object[] outcomes)
    {
        var chatClient = new QueueChatClient(outcomes);
        var model = new StubAgentModel(chatClient);
        var executor = new AgentExecutor(
            new StubAgentModelResolver(model),
            NullLogger<AgentExecutor>.Instance);

        return new AgentWorkflow(
            new FixerAgent(executor),
            new ReviewerAgent(executor),
            new DeveloperAgent(executor),
            new PlannerAgent(executor));
    }

    private static void AssertStep(
        AgentExecutionResult step,
        RunContext run,
        string agentName,
        string stepId,
        int attempt)
    {
        Assert.Equal(StepStatus.Succeeded, step.Status);
        Assert.Equal(run.RunId, step.Context.RunId);
        Assert.Equal(run.WorkflowId, step.Context.WorkflowId);
        Assert.Equal(stepId, step.Context.StepId);
        Assert.Equal(attempt, step.Context.Attempt);
        Assert.Equal(agentName, step.AgentName);
        Assert.Equal("test-profile", step.Model?.ProfileName);
        Assert.Equal(11, step.Usage.InputTokens);
        Assert.Equal(7, step.Usage.OutputTokens);
        Assert.Equal(18, step.Usage.TotalTokens);
        Assert.Equal("stop", step.FinishReason);
        Assert.True(step.Duration >= TimeSpan.Zero);
    }

    private const string PlannerResponse =
        """
        {
          "goal": "Build a todo API.",
          "steps": [
            { "id": 1, "description": "Implement the endpoint." }
          ]
        }
        """;

    private const string ApprovedReview =
        """
        {
          "approved": true,
          "issues": []
        }
        """;

    private const string RejectedReview =
        """
        {
          "approved": false,
          "issues": [
            { "severity": "high", "description": "A required endpoint is missing." }
          ]
        }
        """;

    private sealed class StubAgentModel(IChatClient chatClient) : IAgentModel
    {
        public AgentModelDescriptor Descriptor { get; } =
            new("test-profile", "Test", "test-model");

        public IChatClient ChatClient { get; } = chatClient;

        public void ConfigureOptions(string agentName, ChatOptions options)
        {
        }
    }

    private sealed class StubAgentModelResolver(IAgentModel model) : IAgentModelResolver
    {
        public IAgentModel Resolve(string agentName) => model;
    }

    private sealed class QueueChatClient(IEnumerable<object> outcomes) : IChatClient
    {
        private readonly Queue<object> _outcomes = new(outcomes);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (_outcomes.Count == 0)
            {
                throw new InvalidOperationException("No fake response remains.");
            }

            var outcome = _outcomes.Dequeue();

            if (outcome is Exception exception)
            {
                return Task.FromException<ChatResponse>(exception);
            }

            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, (string)outcome))
            {
                FinishReason = ChatFinishReason.Stop,
                Usage = new UsageDetails
                {
                    InputTokenCount = 11,
                    OutputTokenCount = 7,
                    TotalTokenCount = 18
                }
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}

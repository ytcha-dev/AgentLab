using AgentLab.Core.Agents;
using AgentLab.Core.Contracts;
using AgentLab.Core.Models;
using AgentLab.Core.Orchestration;
using AgentLab.Core.Runtime;
using AgentLab.Infrastructure.ModelProviders;
using Microsoft.Extensions.AI;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
{
    builder.Services.AddAgentModelProviders(builder.Configuration);
    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.Converters.Add(
            new JsonStringEnumConverter()));

    builder.Services.AddScoped<PlannerAgent>();
    builder.Services.AddScoped<DeveloperAgent>();
    builder.Services.AddScoped<ReviewerAgent>();
    builder.Services.AddScoped<FixerAgent>();
    builder.Services.AddScoped<AgentWorkflow>();
    builder.Services.AddSingleton<AgentExecutor>();
}

var app = builder.Build();
{
    Console.WriteLine(
        $"BUILD MARKER: {DateTime.Now:HH:mm:ss}");

    app.UseHttpsRedirection();

    app.MapGet("/ping", () => Results.Ok("pong"));

    app.MapGet("/ai/model/{agentName}", (
        string agentName,
        IAgentModelResolver models) =>
    {
        return Results.Ok(models.Resolve(agentName).Descriptor);
    });

    app.MapGet("/ai/test", async (
        AgentExecutor executor,
        CancellationToken cancellationToken) =>
    {
        var run = RunContext.Create("ai-test");
        var execution = await executor.ExecuteAsync(
            run.ForStep("model-call"),
            "test",
            [new ChatMessage(ChatRole.User, "Hello, AI!")],
            new ChatOptions { MaxOutputTokens = 100 },
            cancellationToken);

        return Results.Ok(new
        {
            execution,
            response = execution.Output
        });
    });

    app.MapPost("/agent/plan", async (
        CodingTask task,
        PlannerAgent planner,
        CancellationToken cancellationToken) =>
    {
        var run = RunContext.Create("agent-plan");
        var plan = await planner.ExecuteAsync(
            task,
            run.ForStep("plan"),
            cancellationToken);

        return Results.Ok(new { run, plan });
    });

    app.MapPost("/agent/develop", async (
        CodingTask task,
        PlannerAgent planner,
        DeveloperAgent developer,
        CancellationToken cancellationToken) =>
    {
        var run = RunContext.Create("agent-develop");
        var plan = await planner.ExecuteAsync(
            task,
            run.ForStep("plan"),
            cancellationToken);

        AgentStepResult<Implementation>? implementation = null;

        if (plan.IsSuccess)
        {
            implementation = await developer.ExecuteAsync(
                new DeveloperInput(task, plan.Value!),
                run.ForStep("develop"),
                cancellationToken);
        }

        return Results.Ok(new { run, plan, implementation });
    });

    app.MapPost("/agent/review", async (
        CodingTask task,
        PlannerAgent planner,
        DeveloperAgent developer,
        ReviewerAgent reviewer,
        CancellationToken cancellationToken) =>
    {
        var run = RunContext.Create("agent-review");
        var plan = await planner.ExecuteAsync(
            task,
            run.ForStep("plan"),
            cancellationToken);

        AgentStepResult<Implementation>? implementation = null;
        AgentStepResult<ReviewResult>? review = null;

        if (plan.IsSuccess)
        {
            implementation = await developer.ExecuteAsync(
                new DeveloperInput(task, plan.Value!),
                run.ForStep("develop"),
                cancellationToken);
        }

        if (implementation?.IsSuccess == true)
        {
            review = await reviewer.ExecuteAsync(
                new ReviewInput(
                    task,
                    plan.Value!,
                    implementation.Value!),
                run.ForStep("review"),
                cancellationToken);
        }

        return Results.Ok(new
        {
            run,
            plan,
            implementation,
            review
        });
    });

    app.MapPost("/agent/run", async (
        CodingTask task,
        AgentWorkflow workflow,
        CancellationToken cancellationToken) =>
    {
        var result = await workflow.ExecuteAsync(
            task,
            cancellationToken);

        return Results.Ok(result);
    });

    app.Run();
}

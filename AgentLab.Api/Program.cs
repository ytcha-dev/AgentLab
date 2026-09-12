using AgentLab.Core.Agents;
using AgentLab.Core.Contracts;
using AgentLab.Core.Models;
using AgentLab.Core.Orchestration;
using AgentLab.Core.Runtime;
using AgentLab.Infrastructure.ModelProviders;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);
{
    builder.Services.AddAgentModelProviders(builder.Configuration);

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
        var response = await executor.ExecuteAsync(
            "test",
            [new ChatMessage(ChatRole.User, "Hello, AI!")],
            new ChatOptions { MaxOutputTokens = 100 },
            cancellationToken);

        return Results.Ok(new
        {
            response = response.Text
        });
    });

    app.MapPost("/agent/plan", async (
        CodingTask task,
        PlannerAgent planner,
        CancellationToken cancellationToken) =>
    {
        var plan = await planner.ExecuteAsync(
            task,
            cancellationToken);

        return Results.Ok(plan);
    });

    app.MapPost("/agent/develop", async (
        CodingTask task,
        PlannerAgent planner,
        DeveloperAgent developer,
        CancellationToken cancellationToken) =>
    {
        var plan = await planner.ExecuteAsync(
            task,
            cancellationToken);

        var implementation = await developer.ExecuteAsync(
            new DeveloperInput(task, plan),
            cancellationToken);

        return Results.Ok(implementation);
    });

    app.MapPost("/agent/review", async (
        CodingTask task,
        PlannerAgent planner,
        DeveloperAgent developer,
        ReviewerAgent reviewer,
        CancellationToken cancellationToken) =>
    {
        var plan = await planner.ExecuteAsync(
            task,
            cancellationToken);

        var implementation = await developer.ExecuteAsync(
            new DeveloperInput(task, plan),
            cancellationToken);

        var review = await reviewer.ExecuteAsync(
            new ReviewInput(task, plan, implementation),
            cancellationToken);

        return Results.Ok(new
        {
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

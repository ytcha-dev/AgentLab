using AgentLab.Core.Agents;
using AgentLab.Core.Models;
using AgentLab.Core.Orchestration;
using Microsoft.Extensions.AI;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);
{
    builder.Services.AddHttpClient("ollama", client =>
    {
        client.BaseAddress = new Uri("http://localhost:11434");
        client.Timeout = TimeSpan.FromMinutes(5);
    });

    builder.Services.AddSingleton<IChatClient>(sp =>
    {
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = factory.CreateClient("ollama");

        return new OllamaApiClient(
            httpClient,
            "qwen3.5:9b");
    });

    builder.Services.AddScoped<PlannerAgent>();
    builder.Services.AddScoped<DeveloperAgent>();
    builder.Services.AddScoped<ReviewerAgent>();
    builder.Services.AddScoped<FixerAgent>();
    builder.Services.AddScoped<AgentWorkflow>();
}

var app = builder.Build();
{
    Console.WriteLine(
        $"BUILD MARKER: {DateTime.Now:HH:mm:ss}");

    app.UseHttpsRedirection();

    app.MapGet("/ping", () => Results.Ok("pong"));

    app.MapGet("/ai/test", async (IChatClient chatClient) =>
    {
        var response = await chatClient.GetResponseAsync("Hello, AI!");
        return Results.Ok(new
        {
            response
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
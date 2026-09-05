using Microsoft.Extensions.AI;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);
// Configure services here, e.g., builder.Services.AddControllers();
{
    var ollama = new OllamaApiClient(
        new Uri("http://localhost:11434"),
        "qwen3.5:9b"
    );

    builder.Services.AddSingleton<IChatClient>(ollama);
}

var app = builder.Build();
// pipeline, middleware, routing, etc.
{
    app.UseHttpsRedirection();

    app.MapGet("/ai/test", async (IChatClient chatClient) =>
    {
        var response = await chatClient.GetResponseAsync("Hello, AI!");
        return Results.Ok(new
        {
            response = response
        });
    });

    app.Run();
}
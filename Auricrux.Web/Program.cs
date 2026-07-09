var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();

var app = builder.Build();

// Minimal API - just endpoints, no complex components
app.MapGet("/", () => Results.Ok(new { message = "Auricrux API Running", version = "1.0.0", timestamp = DateTime.UtcNow }));

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", app = "Auricrux", version = "1.0.0", timestamp = DateTime.UtcNow }));

app.MapPost("/api/thinking", async (HttpContext context) =>
{
    try
    {
        var request = await context.Request.ReadFromJsonAsync<dynamic>();
        
        return Results.Ok(new 
        { 
            thinking_output = "Extended thinking about the construction question...",
            analysis = "Comprehensive breakdown of the issue",
            timestamp = DateTime.UtcNow 
        });
    }
    catch
    {
        return Results.BadRequest(new { error = "Invalid request format" });
    }
});

app.MapPost("/api/search", async (HttpContext context) =>
{
    try
    {
        var request = await context.Request.ReadFromJsonAsync<dynamic>();
        
        return Results.Ok(new 
        { 
            results = new[] 
            {
                new { id = "1", title = "Result 1", relevance = 0.95 },
                new { id = "2", title = "Result 2", relevance = 0.87 }
            },
            timestamp = DateTime.UtcNow 
        });
    }
    catch
    {
        return Results.BadRequest(new { error = "Invalid request format" });
    }
});

app.Run();

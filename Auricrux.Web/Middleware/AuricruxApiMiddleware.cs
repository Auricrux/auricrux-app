using Auricrux.Shared.Models;

namespace Auricrux.Web.Middleware;

public class AuricruxApiMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuricruxApiMiddleware> _logger;

    public AuricruxApiMiddleware(RequestDelegate next, ILogger<AuricruxApiMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Debug: Log ALL requests to see if middleware is being invoked
        _logger.LogWarning("=== AURICRUX MIDDLEWARE INVOKED === Path: {Path}, Method: {Method}", context.Request.Path, context.Request.Method);

        // Only intercept /api/ requests
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            _logger.LogInformation("API request intercepted: {Method} {Path}", context.Request.Method, context.Request.Path);

            if (context.Request.Path == "/api/health" && context.Request.Method == "GET")
            {
                await HandleHealth(context);
                return;
            }
            else if (context.Request.Path == "/api/thinking" && context.Request.Method == "POST")
            {
                await HandleThinking(context);
                return;
            }
            else if (context.Request.Path == "/api/search" && context.Request.Method == "POST")
            {
                await HandleSearch(context);
                return;
            }
        }

        await _next(context);
    }

    private static async Task HandleHealth(HttpContext context)
    {
        // Test: Return plain text first to see if middleware is invoked
        context.Response.ContentType = "text/plain";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync("Auricrux API Middleware is ACTIVE - Health: OK");
    }

    private static async Task HandleThinking(HttpContext context)
    {
        var request = await context.Request.ReadFromJsonAsync<ThinkingRequest>();
        if (request == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var response = new ThinkingResponse
        {
            Success = true,
            Mode = request.Mode,
            Result = $"Thinking response for query: {request.Query}",
            ProcessingTimeMs = Random.Shared.Next(500, 3000),
            Timestamp = DateTime.UtcNow
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsJsonAsync(response);
    }

    private static async Task HandleSearch(HttpContext context)
    {
        var request = await context.Request.ReadFromJsonAsync<SearchRequest>();
        if (request == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var response = new SearchResponse
        {
            Success = true,
            Scope = request.Scope,
            Results = new List<SearchResult>
            {
                new() { Title = "Result 1", Snippet = "Sample search result for query: " + request.Query, Score = 0.95 },
                new() { Title = "Result 2", Snippet = "Another relevant result", Score = 0.87 },
                new() { Title = "Result 3", Snippet = "Additional search result", Score = 0.76 }
            },
            TotalResults = 3,
            Timestamp = DateTime.UtcNow
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsJsonAsync(response);
    }
}

public static class AuricruxApiExtensions
{
    public static IApplicationBuilder UseAuricruxApi(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuricruxApiMiddleware>();
    }
}


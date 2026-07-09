using Auricrux.Web.Components;
using Auricrux.Web.Middleware;
using Auricrux.Shared.Services;
using Auricrux.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure Auricrux settings
var auricruxConfig = new AuricruxConfig
{
    ApiEndpoint = builder.Configuration["Auricrux:ApiEndpoint"] ?? "http://localhost:5000",
    DefaultThinkingMode = ThinkingMode.Auto,
    DefaultSearchScope = SearchScope.Both,
    EnableAutoSpeak = false,
    TimeoutSeconds = 30,
    EnableLogging = true
};

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

// Register Auricrux services
builder.Services
    .AddLogging(logging =>
    {
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .AddSingleton(auricruxConfig)
    .AddHttpClient<AuricruxApiClient>()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri(auricruxConfig.ApiEndpoint);
        client.Timeout = TimeSpan.FromSeconds(auricruxConfig.TimeoutSeconds);
    });

builder.Services
    .AddSingleton<TextToSpeechService>()
    .AddScoped<AuricruxService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseHttpsRedirection();

// Handle /api routes with MIDDLEWARE (before routing)
app.Use(async (context, next) =>
{
    var log = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var path = context.Request.Path.Value ?? "";
    
    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
    {
        log.LogInformation("API Request: {Method} {Path}", context.Request.Method, path);
        
        // Explicitly set content type first
        context.Response.ContentType = "application/json";
        
        if (path.Equals("/api/health", StringComparison.OrdinalIgnoreCase) && context.Request.Method == "GET")
        {
            context.Response.StatusCode = 200;
            await context.Response.WriteAsJsonAsync(new { status = "healthy", app = "Auricrux", timestamp = DateTime.UtcNow });
            return;
        }
        else if (path.Equals("/api/thinking", StringComparison.OrdinalIgnoreCase) && context.Request.Method == "POST")
        {
            try
            {
                var req = await context.Request.ReadFromJsonAsync<ThinkingRequest>();
                context.Response.StatusCode = 200;
                await context.Response.WriteAsJsonAsync(new ThinkingResponse
                {
                    Success = true,
                    Mode = req?.Mode ?? ThinkingMode.Auto,
                    Result = $"Response to: {req?.Query}",
                    ProcessingTimeMs = Random.Shared.Next(500, 3000),
                    Timestamp = DateTime.UtcNow
                });
                return;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error processing thinking request");
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid request" });
                return;
            }
        }
        else if (path.Equals("/api/search", StringComparison.OrdinalIgnoreCase) && context.Request.Method == "POST")
        {
            try
            {
                var req = await context.Request.ReadFromJsonAsync<SearchRequest>();
                context.Response.StatusCode = 200;
                await context.Response.WriteAsJsonAsync(new SearchResponse
                {
                    Success = true,
                    Scope = req?.Scope ?? SearchScope.Both,
                    Results = new List<SearchResult>
                    {
                        new() { Title = "Result 1", Snippet = $"Search result for: {req?.Query}", Score = 0.95 },
                        new() { Title = "Result 2", Snippet = "Another result", Score = 0.87 },
                        new() { Title = "Result 3", Snippet = "Third result", Score = 0.76 }
                    },
                    TotalResults = 3,
                    Timestamp = DateTime.UtcNow
                });
                return;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error processing search request");
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid request" });
                return;
            }
        }
        else
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = "API endpoint not found", path });
            return;
        }
    }
    
    await next();
});

app.UseAntiforgery();

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    // Static files
    endpoints.MapStaticAssets();

    // Razor Components - for Blazor rendering (everything else)
    endpoints.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();
});

app.Run();

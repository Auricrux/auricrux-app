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

app.UseAntiforgery();

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    // API endpoints MUST be mapped first
    endpoints.MapPost("/api/thinking", async (ThinkingRequest req, ILogger<Program> log) =>
    {
        log.LogInformation("Thinking: {Query}", req.Query);
        return Results.Ok(new ThinkingResponse
        {
            Success = true,
            Mode = req.Mode,
            Result = $"Response to: {req.Query}",
            ProcessingTimeMs = Random.Shared.Next(500, 3000),
            Timestamp = DateTime.UtcNow
        });
    });

    endpoints.MapPost("/api/search", async (SearchRequest req, ILogger<Program> log) =>
    {
        log.LogInformation("Search: {Query}", req.Query);
        return Results.Ok(new SearchResponse
        {
            Success = true,
            Scope = req.Scope,
            Results = new List<SearchResult>
            {
                new() { Title = "Result 1", Snippet = $"Search result for: {req.Query}", Score = 0.95 },
                new() { Title = "Result 2", Snippet = "Another result", Score = 0.87 },
                new() { Title = "Result 3", Snippet = "Third result", Score = 0.76 }
            },
            TotalResults = 3,
            Timestamp = DateTime.UtcNow
        });
    });

    endpoints.MapGet("/api/health", () => Results.Ok(new { status = "healthy", app = "Auricrux", timestamp = DateTime.UtcNow }));

    // Map static assets
    endpoints.MapStaticAssets();

    // DISABLE: Razor Components - testing if this is causing the issue
    // endpoints.MapRazorComponents<App>()
    //     .AddInteractiveServerRenderMode();
});

app.Run();

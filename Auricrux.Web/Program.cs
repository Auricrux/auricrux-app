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

// Enable detailed logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseHttpsRedirection();

// Debug middleware - log EVERY request
app.Use(async (context, next) =>
{
    var log = context.RequestServices.GetRequiredService<ILogger<Program>>();
    log.LogInformation("[DEBUG] Request: {Method} {Path} {Host}", context.Request.Method, context.Request.Path, context.Request.Host);
    await next();
});

// Handle API routes with MIDDLEWARE (before routing)
app.Use(async (context, next) =>
{
    var log = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var path = context.Request.Path.Value ?? "";
    
    // Match both /api/ and /v1/ paths for testing
    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase))
    {
        log.LogInformation("[API] Handling {Path}", path);
        context.Response.ContentType = "application/json";
        
        if ((path.Equals("/api/health", StringComparison.OrdinalIgnoreCase) || path.Equals("/v1/health", StringComparison.OrdinalIgnoreCase)) && context.Request.Method == "GET")
        {
            context.Response.StatusCode = 200;
            await context.Response.WriteAsJsonAsync(new { status = "healthy", app = "Auricrux", timestamp = DateTime.UtcNow });
            return;
        }
        else
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = "Not found", path });
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

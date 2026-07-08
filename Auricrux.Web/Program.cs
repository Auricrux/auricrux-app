using Auricrux.Web.Components;
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

// Add services to the container.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

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
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

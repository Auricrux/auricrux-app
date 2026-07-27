using Auricrux.Shared.Models;
using Auricrux.Shared.Services;
using Auricrux.Web.Components;
using Auricrux.Web.Services;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddHttpClient(nameof(ConstructionIntelligenceService));
builder.Services.AddHttpClient(nameof(MediaGenerationService));
builder.Services.AddHttpClient(nameof(FcaAccountLinkService));
builder.Services.AddSingleton<ConstructionIntelligenceService>();
builder.Services.AddSingleton<MediaGenerationService>();
builder.Services.AddSingleton<WorkspaceStorageService>();
builder.Services.AddSingleton<ConversationMemoryService>();
builder.Services.AddSingleton<FcaAccountLinkService>();
builder.Services.AddSingleton(sp =>
{
    var config = new AuricruxConfig
    {
        ApiEndpoint = builder.Configuration["Auricrux:PublicBaseUrl"] ?? "http://localhost:5080/",
        EnableAutoSpeak = false,
        EnableLogging = true,
        TimeoutSeconds = 180
    };
    return config;
});
builder.Services.AddHttpClient<AuricruxApiClient>((sp, client) =>
{
    var cfg = sp.GetRequiredService<AuricruxConfig>();
    client.BaseAddress = new Uri(cfg.ApiEndpoint);
});
builder.Services.AddSingleton<TextToSpeechService>();
builder.Services.AddScoped<AuricruxService>();

var app = builder.Build();

var mediaRoot = Path.Combine(app.Environment.ContentRootPath, "Data", "media");
Directory.CreateDirectory(mediaRoot);
var workspaceRoot = Path.Combine(app.Environment.ContentRootPath, "Data", "workspace");
Directory.CreateDirectory(workspaceRoot);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaRoot),
    RequestPath = "/media"
});
app.UseAntiforgery();
app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

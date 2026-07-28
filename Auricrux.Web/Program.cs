using System.Threading.RateLimiting;
using Auricrux.Shared.Models;
using Auricrux.Shared.Services;
using Auricrux.Web.Components;
using Auricrux.Web.Middleware;
using Auricrux.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddHttpClient(nameof(ConstructionIntelligenceService));
builder.Services.AddHttpClient(nameof(MediaGenerationService));
builder.Services.AddHttpClient(nameof(FcaAccountLinkService));
builder.Services.AddHttpClient(nameof(BackendHealthService));
builder.Services.AddSingleton<ConstructionIntelligenceService>();
builder.Services.AddSingleton<MediaGenerationService>();
builder.Services.AddSingleton<WorkspaceStorageService>();
builder.Services.AddSingleton<ConversationMemoryService>();
builder.Services.AddSingleton<FcaAccountLinkService>();
builder.Services.AddSingleton<FreemiumAccountStore>();
builder.Services.AddSingleton<BackendHealthService>();
builder.Services.AddSingleton(sp =>
{
    var configured = builder.Configuration["Auricrux:ApiEndpoint"]
        ?? builder.Configuration["Auricrux:PublicBaseUrl"]
        ?? "http://localhost:5080/";
    return new AuricruxConfig
    {
        ApiEndpoint = configured.TrimEnd('/') + "/",
        EnableAutoSpeak = false,
        EnableLogging = true,
        TimeoutSeconds = 180
    };
});
builder.Services.AddHttpClient<AuricruxApiClient>((sp, client) =>
{
    var cfg = sp.GetRequiredService<AuricruxConfig>();
    client.BaseAddress = new Uri(cfg.ApiEndpoint);
});
builder.Services.AddSingleton<TextToSpeechService>();
builder.Services.AddScoped<AuricruxService>();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5080", "https://localhost:7080", "https://auricrux.futurecontractorsofamerica.com"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("auricrux", policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("api", httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimit:PermitLimit", 120),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

var authEnabled = builder.Configuration.GetValue("Auth:Enabled", false);
var authAuthority = builder.Configuration["Auth:Authority"];
if (authEnabled && !string.IsNullOrWhiteSpace(authAuthority))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = authAuthority.TrimEnd('/');
            options.Audience = builder.Configuration["Auth:Audience"] ?? "auricrux";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = !string.IsNullOrWhiteSpace(builder.Configuration["Auth:Audience"]),
                ValidateIssuer = true
            };
        });
    builder.Services.AddAuthorization();
}

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
app.UseAuricruxApiMiddleware();
app.UseCors("auricrux");
app.UseRateLimiter();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaRoot),
    RequestPath = "/media"
});
app.UseAntiforgery();

if (authEnabled && !string.IsNullOrWhiteSpace(authAuthority))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapStaticAssets();
app.MapControllers().RequireRateLimiting("api");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;

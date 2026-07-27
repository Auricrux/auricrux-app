using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Auricrux.Web.Services;

/// <summary>
/// Sovereign image + video generation for construction specialists.
/// Prefers local Stable Diffusion / ComfyUI when configured; always has offline construction SVG/PNG + storyboard fallbacks.
/// </summary>
public sealed class MediaGenerationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<MediaGenerationService> _logger;

    public MediaGenerationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        IWebHostEnvironment env,
        ILogger<MediaGenerationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _env = env;
        _logger = logger;
    }

    public async Task<MediaArtifact> GenerateImageAsync(string prompt, CancellationToken ct = default)
    {
        Directory.CreateDirectory(MediaRoot);
        var id = Guid.NewGuid().ToString("N");
        var sdUrl = _config["Auricrux:StableDiffusionUrl"];
        if (!string.IsNullOrWhiteSpace(sdUrl))
        {
            try
            {
                var client = _httpClientFactory.CreateClient(nameof(MediaGenerationService));
                var payload = new
                {
                    prompt = $"construction site technical illustration, professional, {prompt}",
                    steps = 20,
                    width = 1024,
                    height = 768
                };
                using var response = await client.PostAsJsonAsync($"{sdUrl.TrimEnd('/')}/sdapi/v1/txt2img", payload, ct);
                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    if (doc.RootElement.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
                    {
                        var bytes = Convert.FromBase64String(images[0].GetString()!);
                        var path = Path.Combine(MediaRoot, $"{id}.png");
                        await File.WriteAllBytesAsync(path, bytes, ct);
                        return new MediaArtifact(id, "image", path, $"/media/{id}.png", "stable-diffusion", prompt);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stable Diffusion unavailable; using offline construction renderer");
            }
        }

        var svgPath = Path.Combine(MediaRoot, $"{id}.svg");
        await File.WriteAllTextAsync(svgPath, BuildConstructionSvg(prompt), ct);
        return new MediaArtifact(id, "image", svgPath, $"/media/{id}.svg", "offline-svg", prompt);
    }

    public async Task<MediaArtifact> GenerateVideoAsync(string prompt, int frames = 8, CancellationToken ct = default)
    {
        Directory.CreateDirectory(MediaRoot);
        var id = Guid.NewGuid().ToString("N");
        var storyDir = Path.Combine(MediaRoot, $"{id}_frames");
        Directory.CreateDirectory(storyDir);

        frames = Math.Clamp(frames, 4, 24);
        for (var i = 0; i < frames; i++)
        {
            var framePrompt = $"{prompt} — construction sequence step {i + 1}/{frames}";
            var framePath = Path.Combine(storyDir, $"frame_{i:D3}.svg");
            await File.WriteAllTextAsync(framePath, BuildConstructionSvg(framePrompt, step: i + 1, total: frames), ct);
        }

        var manifestPath = Path.Combine(storyDir, "storyboard.json");
        var manifest = new
        {
            id,
            prompt,
            frames,
            generatedAt = DateTime.UtcNow,
            note = "Storyboard package. If ffmpeg is installed, Auricrux stitches frames to MP4."
        };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), ct);

        var mp4Path = Path.Combine(MediaRoot, $"{id}.mp4");
        var stitched = await TryFfmpegStitchAsync(storyDir, mp4Path, ct);
        if (stitched)
        {
            return new MediaArtifact(id, "video", mp4Path, $"/media/{id}.mp4", "ffmpeg-storyboard", prompt);
        }

        var zipNote = Path.Combine(MediaRoot, $"{id}.storyboard.txt");
        await File.WriteAllTextAsync(zipNote, $"Storyboard ready at {storyDir} ({frames} frames). Install ffmpeg to auto-stitch MP4.", ct);
        return new MediaArtifact(id, "video-storyboard", storyDir, $"/media/{id}_frames/storyboard.json", "offline-storyboard", prompt);
    }

    private string MediaRoot => Path.Combine(_env.ContentRootPath, "Data", "media");

    private static async Task<bool> TryFfmpegStitchAsync(string frameDir, string mp4Path, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -framerate 2 -i \"{Path.Combine(frameDir, "frame_%03d.svg")}\" -vf format=yuv420p \"{mp4Path}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0 && File.Exists(mp4Path);
        }
        catch
        {
            return false;
        }
    }

    private static string BuildConstructionSvg(string prompt, int step = 1, int total = 1)
    {
        var safe = System.Security.SecurityElement.Escape(prompt.Length > 120 ? prompt[..120] + "…" : prompt) ?? "construction";
        var barWidth = (int)(900.0 * step / Math.Max(total, 1));
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="1024" height="768" viewBox="0 0 1024 768">
              <defs>
                <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0%" stop-color="#0f1c18"/>
                  <stop offset="100%" stop-color="#1a2e28"/>
                </linearGradient>
              </defs>
              <rect width="1024" height="768" fill="url(#g)"/>
              <rect x="64" y="120" width="896" height="420" fill="#f4f1ea" opacity="0.92"/>
              <text x="80" y="90" fill="#c4a35a" font-family="Georgia, serif" font-size="28">Auricrux Construction Visual</text>
              <text x="80" y="180" fill="#111" font-family="Segoe UI, sans-serif" font-size="20">{safe}</text>
              <rect x="80" y="220" width="200" height="280" fill="#c4a35a" opacity="0.35"/>
              <polygon points="300,500 520,260 740,500" fill="#1a2e28" opacity="0.55"/>
              <rect x="80" y="560" width="864" height="18" fill="#ddd"/>
              <rect x="80" y="560" width="{barWidth}" height="18" fill="#c4a35a"/>
              <text x="80" y="620" fill="#f4f1ea" font-family="Segoe UI, sans-serif" font-size="16">Step {step}/{total} · offline sovereign renderer (SD endpoint optional)</text>
            </svg>
            """;
    }
}

public sealed record MediaArtifact(
    string Id,
    string Kind,
    string LocalPath,
    string PublicPath,
    string Engine,
    string Prompt);

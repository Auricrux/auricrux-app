using System.Net.Http.Json;
using System.Text.Json;
using Auricrux.Shared.Models;

namespace Auricrux.Web.Services;

/// <summary>
/// Construction field-photo analysis (AUX-001/002 vision gap).
/// Prefers a local Ollama vision model when configured; always returns a structured
/// field-intake + corpus-grounded RFI/punch checklist so the wedge works offline.
/// </summary>
public sealed class ConstructionVisionService(
    ConstructionIntelligenceService intelligence,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<ConstructionVisionService> logger)
{
    private const int MaxImageBytes = 8_000_000;

    public async Task<VisionAnalysisResponse> AnalyzeAsync(VisionAnalysisRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            return Fail("ImageBase64 is required.");
        }

        byte[] bytes;
        try
        {
            var raw = request.ImageBase64.Trim();
            var comma = raw.IndexOf(',');
            if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            {
                raw = raw[(comma + 1)..];
            }

            bytes = Convert.FromBase64String(raw);
        }
        catch (FormatException)
        {
            return Fail("ImageBase64 is not valid base64.");
        }

        if (bytes.Length == 0)
        {
            return Fail("Image is empty.");
        }

        if (bytes.Length > MaxImageBytes)
        {
            return Fail($"Image exceeds {MaxImageBytes / 1_000_000} MB limit.");
        }

        var meta = ReadImageMeta(bytes);
        if (meta.Format == "unknown")
        {
            return Fail("Unsupported image format. Use JPEG, PNG, GIF, or WebP.");
        }

        var prompt = string.IsNullOrWhiteSpace(request.Prompt)
            ? "Analyze this construction field photo for safety hazards, workmanship quality, and RFI-worthy conflicts."
            : request.Prompt.Trim();
        var focus = string.IsNullOrWhiteSpace(request.Focus) ? "field" : request.Focus.Trim().ToLowerInvariant();

        var corpus = intelligence.Search(new SearchRequest { Query = prompt, Scope = SearchScope.Both });
        var checklist = BuildFieldChecklist(focus, prompt, corpus.Results.Take(5).Select(r => r.Title).ToList());
        var rfiDraft = BuildRfiDraft(prompt, focus, meta, checklist);

        var visionModel = config["Auricrux:VisionModel"] ?? "";
        string? llmAnalysis = null;
        var engine = "field-intake-checklist";

        if (!string.IsNullOrWhiteSpace(visionModel))
        {
            using var visionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            visionCts.CancelAfter(TimeSpan.FromSeconds(12));
            try
            {
                llmAnalysis = await TryVisionModelAsync(visionModel, bytes, prompt, focus, visionCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Vision model timed out; using field-intake checklist");
            }

            if (!string.IsNullOrWhiteSpace(llmAnalysis))
            {
                engine = $"ollama-vision:{visionModel}";
            }
        }

        // If no dedicated vision model, still try primary text model with meta + checklist context
        // (honest: not pixel vision — construction-grounded intake synthesis). Short timeout so
        // unreachable Ollama never hangs the field-photo path.
        if (llmAnalysis is null)
        {
            using var synthCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            synthCts.CancelAfter(TimeSpan.FromSeconds(8));
            try
            {
                llmAnalysis = await TryTextSynthesisAsync(prompt, focus, meta, checklist, synthCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                logger.LogDebug("Text synthesis timed out; using offline checklist analysis");
            }
        }

        if (llmAnalysis is not null && engine.StartsWith("field-", StringComparison.Ordinal))
        {
            engine = "field-intake-llm-synthesis";
        }

        return new VisionAnalysisResponse
        {
            Success = true,
            Prompt = prompt,
            Focus = focus,
            Engine = engine,
            Image = meta,
            Analysis = llmAnalysis ?? BuildOfflineAnalysis(prompt, focus, meta, checklist),
            Checklist = checklist,
            RfiDraft = rfiDraft,
            CorpusSources = corpus.Results.Take(5).Select(r => r.Title).ToList(),
            Honesty =
                engine.StartsWith("ollama-vision", StringComparison.Ordinal)
                    ? "Pixel-level vision via local Ollama multimodal model plus construction checklist."
                    : "Structured construction field-photo intake (metadata + corpus checklist + RFI draft). " +
                      "Set Auricrux:VisionModel (e.g. llava/moondream) for pixel-level vision."
        };
    }

    private async Task<string?> TryVisionModelAsync(
        string model,
        byte[] bytes,
        string prompt,
        string focus,
        CancellationToken ct)
    {
        var ollamaBase = (config["Auricrux:OllamaUrl"] ?? "http://127.0.0.1:11434").TrimEnd('/');
        var system =
            "You are Auricrux construction field vision. Describe what you see on a job site photo. " +
            "Flag OSHA 1926 hazards, workmanship defects, missing PPE, and RFI-worthy conflicts. " +
            "Be specific. Do not invent code section numbers. Prefer actionable field language.";
        var user =
            $"Focus: {focus}\nOperator question: {prompt}\n" +
            "Return: (1) what is visible, (2) hazards/defects, (3) recommended next field action.";

        try
        {
            var http = httpClientFactory.CreateClient(nameof(ConstructionIntelligenceService));
            using var response = await http.PostAsJsonAsync(
                $"{ollamaBase}/api/chat",
                new
                {
                    model,
                    stream = false,
                    messages = new object[]
                    {
                        new { role = "system", content = system },
                        new
                        {
                            role = "user",
                            content = user,
                            images = new[] { Convert.ToBase64String(bytes) }
                        }
                    }
                },
                ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Vision model {Model} returned {Status}", model, response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return doc.RootElement.GetProperty("message").GetProperty("content").GetString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Vision model {Model} unavailable", model);
            return null;
        }
    }

    private async Task<string?> TryTextSynthesisAsync(
        string prompt,
        string focus,
        ImageMeta meta,
        IReadOnlyList<string> checklist,
        CancellationToken ct)
    {
        var ollamaBase = (config["Auricrux:OllamaUrl"] ?? "http://127.0.0.1:11434").TrimEnd('/');
        var model = config["Auricrux:PrimaryModel"] ?? "auricrux-fca";
        var system =
            "You are Auricrux construction field-photo intake. You do NOT see pixels in this mode. " +
            "Given image metadata + a construction checklist, produce a tight field brief and RFI-ready notes. " +
            "Say explicitly that pixel vision was not used.";
        var user =
            $"Focus: {focus}\nPrompt: {prompt}\nImage: {meta.Format} {meta.Width}x{meta.Height} ({meta.Bytes} bytes)\n" +
            "Checklist:\n- " + string.Join("\n- ", checklist);

        try
        {
            var http = httpClientFactory.CreateClient(nameof(ConstructionIntelligenceService));
            using var response = await http.PostAsJsonAsync(
                $"{ollamaBase}/api/chat",
                new
                {
                    model,
                    stream = false,
                    messages = new object[]
                    {
                        new { role = "system", content = system },
                        new { role = "user", content = user }
                    }
                },
                ct);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return doc.RootElement.GetProperty("message").GetProperty("content").GetString();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Text synthesis for vision intake unavailable");
            return null;
        }
    }

    private static string BuildOfflineAnalysis(
        string prompt,
        string focus,
        ImageMeta meta,
        IReadOnlyList<string> checklist) =>
        $"""
        Auricrux construction field-photo intake (offline / no vision model reachable).

        Image: {meta.Format.ToUpperInvariant()} {meta.Width}×{meta.Height}, {meta.Bytes:N0} bytes.
        Focus: {focus}
        Operator question: {prompt}

        Field checklist:
        {string.Join("\n", checklist.Select((c, i) => $"{i + 1}. {c}"))}

        Next step: walk the shot against the checklist on site, attach this photo to the RFI/punch item, and verify against plans/specs before concealment.
        """;

    private static List<string> BuildFieldChecklist(string focus, string prompt, IReadOnlyList<string> corpusTitles)
    {
        var items = new List<string>();
        var hay = $"{focus} {prompt}".ToLowerInvariant();

        if (hay.Contains("safety") || hay.Contains("osha") || focus is "safety")
        {
            items.AddRange(
            [
                "Confirm fall protection / guardrails where elevation > 6 ft (OSHA 1926 Subpart M).",
                "Verify PPE (hard hat, eye, high-vis, gloves) and competent-person presence.",
                "Check housekeeping: trip hazards, protruding rebar caps, open holes."
            ]);
        }

        if (hay.Contains("punch") || hay.Contains("quality") || focus is "quality" or "punch")
        {
            items.AddRange(
            [
                "Document exact location (grid/room/elevation) and date/time on the photo.",
                "Compare finish level / alignment / plumb against the specified tolerance.",
                "Note whether defect is pre-concealment (hold point) or post-install punch."
            ]);
        }

        if (hay.Contains("rfi") || hay.Contains("conflict") || focus is "rfi")
        {
            items.AddRange(
            [
                "Capture drawing/spec conflict with sheet reference in the RFI subject line.",
                "Propose a solution and state schedule impact if known.",
                "Log issue date — response latency may support delay documentation later."
            ]);
        }

        if (hay.Contains("concrete") || hay.Contains("rebar") || hay.Contains("form"))
        {
            items.Add("Confirm rebar cover, chairs, and form bracing before pour (hold point).");
        }

        if (hay.Contains("scaffold") || hay.Contains("ladder"))
        {
            items.Add("Scaffold: competent person erect/move; guardrails > 10 ft; full planking.");
        }

        foreach (var title in corpusTitles.Take(3))
        {
            items.Add($"Corpus check: {title}");
        }

        if (items.Count == 0)
        {
            items.AddRange(
            [
                "Identify trade and CSI division visible in the photo.",
                "Flag any safety or quality issue before concealment.",
                "Attach photo to work order / RFI / punch with location metadata."
            ]);
        }

        return items.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
    }

    private static string BuildRfiDraft(string prompt, string focus, ImageMeta meta, IReadOnlyList<string> checklist) =>
        $"""
        Subject: Field photo review — {focus} — {Truncate(prompt, 80)}
        Description:
        Attached {meta.Format.ToUpperInvariant()} photo ({meta.Width}×{meta.Height}).
        Operator question: {prompt}

        Observed / checklist items:
        {string.Join("\n", checklist.Select(c => $"- {c}"))}

        Requested clarification / proposed action:
        Please confirm means-and-methods / design intent before proceeding. Attach response to this RFI.
        """;

    private static ImageMeta ReadImageMeta(byte[] bytes)
    {
        // PNG
        if (bytes.Length >= 24
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            var w = ReadInt32Be(bytes, 16);
            var h = ReadInt32Be(bytes, 20);
            return new ImageMeta("png", w, h, bytes.Length);
        }

        // JPEG
        if (bytes.Length > 4 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            var (w, h) = ReadJpegSize(bytes);
            return new ImageMeta("jpeg", w, h, bytes.Length);
        }

        // GIF
        if (bytes.Length >= 10
            && bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F')
        {
            var w = bytes[6] | (bytes[7] << 8);
            var h = bytes[8] | (bytes[9] << 8);
            return new ImageMeta("gif", w, h, bytes.Length);
        }

        // WebP (RIFF....WEBP)
        if (bytes.Length >= 30
            && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
        {
            // VP8X / VP8 / VP8L — best-effort dimensions
            if (bytes[12] == (byte)'V' && bytes[13] == (byte)'P' && bytes[14] == (byte)'8' && bytes[15] == (byte)'X'
                && bytes.Length >= 30)
            {
                var w = 1 + (bytes[24] | (bytes[25] << 8) | (bytes[26] << 16));
                var h = 1 + (bytes[27] | (bytes[28] << 8) | (bytes[29] << 16));
                return new ImageMeta("webp", w, h, bytes.Length);
            }

            return new ImageMeta("webp", 0, 0, bytes.Length);
        }

        return new ImageMeta("unknown", 0, 0, bytes.Length);
    }

    private static (int w, int h) ReadJpegSize(byte[] bytes)
    {
        var i = 2;
        while (i + 9 < bytes.Length)
        {
            if (bytes[i] != 0xFF)
            {
                i++;
                continue;
            }

            var marker = bytes[i + 1];
            if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD9))
            {
                i += 2;
                continue;
            }

            var len = (bytes[i + 2] << 8) | bytes[i + 3];
            if (len < 2 || i + 2 + len > bytes.Length) break;

            // SOF0..SOF3
            if (marker is >= 0xC0 and <= 0xC3)
            {
                var h = (bytes[i + 5] << 8) | bytes[i + 6];
                var w = (bytes[i + 7] << 8) | bytes[i + 8];
                return (w, h);
            }

            i += 2 + len;
        }

        return (0, 0);
    }

    private static int ReadInt32Be(byte[] bytes, int offset) =>
        (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private static VisionAnalysisResponse Fail(string error) =>
        new() { Success = false, Error = error };
}

public sealed class VisionAnalysisRequest
{
    public string? ImageBase64 { get; set; }
    public string? Prompt { get; set; }
    public string? Focus { get; set; }
}

public sealed class VisionAnalysisResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string Focus { get; set; } = string.Empty;
    public string Engine { get; set; } = string.Empty;
    public ImageMeta? Image { get; set; }
    public string Analysis { get; set; } = string.Empty;
    public List<string> Checklist { get; set; } = [];
    public string RfiDraft { get; set; } = string.Empty;
    public List<string> CorpusSources { get; set; } = [];
    public string Honesty { get; set; } = string.Empty;
}

public sealed record ImageMeta(string Format, int Width, int Height, int Bytes);

using System.Text.Json;

namespace Auricrux.Web.Services;

/// <summary>
/// Competitive feature matrix for major-player parity tracking (AUX-001 / AUX-002).
/// Each row is a capability area with honest shipped vs. planned status and per-peer comparison.
/// Model-weight honesty is read from auricrux/system/model_manifest.json (in-place; no alternate stack).
/// </summary>
public sealed class CapabilitiesService(ConstructionIntelligenceService intelligence, IConfiguration config)
{
    private static readonly string[] CompetitorNames = ["ChatGPT", "Claude", "Gemini", "Copilot", "Grok"];

    public CapabilitiesReport GetReport()
    {
        var primaryModel = config["Auricrux:PrimaryModel"] ?? "auricrux-fca";
        var corpusStats = intelligence.GetCorpusStats();
        var weightHonesty = ResolveWeightHonesty();
        var features = BuildFeatureList(corpusStats.TotalEntries, weightHonesty);
        var matrix = BuildCompetitiveMatrix(weightHonesty);
        var shipped = features.Count(f => f.Status == "shipped");
        var planned = features.Count(f => f.Status == "planned");
        var blocked = features.Count(f => f.Status == "blocked");
        var partial = features.Count(f => f.Status == "partial");

        return new CapabilitiesReport
        {
            App = "Auricrux",
            Version = "1.2.0",
            PrimaryModel = primaryModel,
            CorpusEntries = corpusStats.TotalEntries,
            CorpusStats = corpusStats,
            ConstructionMoat = new ConstructionMoatSummary
            {
                SpecialistModel = primaryModel,
                CorpusGroundedSearch = true,
                EvalSuite = "construction_god_suite_v1",
                EvalSuiteLastResult = weightHonesty.EvalSuiteLastResult,
                PromotedFineTuneLive = weightHonesty.FineTuneLive,
                Notes = weightHonesty.Notes
            },
            Platforms = ["Web (Blazor Server)", "Android (MAUI)", "Windows (MAUI)", "iOS (MAUI, macOS host)", "macOS (Mac Catalyst, macOS host)"],
            Features = features,
            Competitors = CompetitorNames,
            CompetitiveMatrix = matrix,
            ParityScore = new ParityScoreSummary
            {
                ShippedCore = shipped,
                Planned = planned + partial,
                Blocked = blocked,
                MatrixRows = matrix.Count,
                AuricruxUniqueAdvantages = matrix.Count(r => r.Auricrux == "shipped" && r.Peers.Values.All(p => p is "no" or "partial")),
                OverallAssessment = weightHonesty.OverallAssessment
            }
        };
    }

    private static WeightHonesty ResolveWeightHonesty()
    {
        // Defaults preserve prior honest posture if manifest is absent.
        var honesty = new WeightHonesty(
            FineTuneLive: false,
            FeatureStatus: "blocked",
            MatrixAuricrux: "blocked",
            EvalSuiteLastResult: "corpus DI 30/30 (2026-07-28); GGUF generative suite not yet recorded",
            Notes: "model_manifest.json not found; cannot assert fine-tune GGUF live.",
            OverallAssessment:
                "FORWARD — major-player feature parity path is live; construction moat unique; " +
                "fine-tune weight status unknown without model_manifest.json.",
            FeatureDetail: "Fine-tune status unknown (manifest missing)",
            MatrixNotes: "Fine-tune status unknown (manifest missing)");

        try
        {
            var path = FindModelManifestPath();
            if (path is null || !File.Exists(path))
            {
                return honesty;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
            var aliasKind = "";
            var aliasNote = "";
            var gguf = "";
            if (root.TryGetProperty("auricruxFcaAlias", out var alias))
            {
                aliasKind = alias.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
                aliasNote = alias.TryGetProperty("note", out var n) ? n.GetString() ?? "" : "";
                gguf = alias.TryGetProperty("ggufObject", out var g) ? g.GetString() ?? "" : "";
            }

            var trainNote = "";
            if (root.TryGetProperty("adapter", out var adapter) &&
                adapter.TryGetProperty("trainProgressNote", out var tp))
            {
                trainNote = tp.GetString() ?? "";
            }

            var notTrueGod = root.TryGetProperty("notTrueGodTierReason", out var nt)
                ? nt.GetString() ?? ""
                : "";

            var mergedLive = status.Contains("product-ollama-loaded", StringComparison.OrdinalIgnoreCase)
                || aliasKind.Contains("merged-lora-gguf", StringComparison.OrdinalIgnoreCase);

            if (!mergedLive)
            {
                return honesty with
                {
                    Notes = "Runtime still on interim alias path per model_manifest.json.",
                    FeatureDetail = "Specialist fine-tune GGUF not loaded (AUX-017)",
                    MatrixNotes = "auricrux-fca not yet serving merged LoRA GGUF (AUX-017)."
                };
            }

            var evalLast = "corpus DI 30/30 (2026-07-28); GGUF generative suite status unknown";
            if (root.TryGetProperty("adapter", out var adapterNode))
            {
                if (adapterNode.TryGetProperty("evalStatus", out var es) && !string.IsNullOrWhiteSpace(es.GetString()))
                {
                    evalLast = es.GetString()!;
                }
                else if (adapterNode.TryGetProperty("ggufGenerativePassRatePercent", out var pr) &&
                         adapterNode.TryGetProperty("ggufGenerativeSuitePassed", out var sp) &&
                         sp.ValueKind == JsonValueKind.True)
                {
                    evalLast = $"corpus DI 30/30 (2026-07-28); GGUF generative live PASS {pr.GetDouble()}% (AUX-019)";
                }
            }

            return new WeightHonesty(
                FineTuneLive: true,
                FeatureStatus: "partial",
                MatrixAuricrux: "partial",
                EvalSuiteLastResult: evalLast,
                Notes:
                    $"Product Ollama auricrux-fca serves merged LoRA GGUF ({gguf}). {aliasNote} {trainNote} " +
                    $"TRUE God final still open: {notTrueGod}".Trim(),
                OverallAssessment:
                    "FORWARD — mid-train specialist GGUF is live in product Ollama (not llama3.2 alias); " +
                    "TRUE God final still requires 297k finish + post-run suites + peer bar (AUX-018/027). GGUF generative ≥80% recorded when evalStatus says PASS.",
                FeatureDetail:
                    $"Merged LoRA GGUF live ({gguf}); TRUE God final pending train finish (AUX-018)",
                MatrixNotes:
                    $"Merged LoRA GGUF live in product ({gguf}); mid-train — not TRUE God final until train finish.");
        }
        catch
        {
            return honesty;
        }
    }

    private static string? FindModelManifestPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "auricrux", "system", "model_manifest.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "auricrux", "system", "model_manifest.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "auricrux", "system", "model_manifest.json")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "auricrux", "system", "model_manifest.json"))
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static List<CapabilityFeature> BuildFeatureList(int corpusEntries, WeightHonesty weights) =>
    [
        Feature("Multi-model chat", "shipped", "User-selectable models with freemium gating"),
        Feature("Thinking modes (Quick/Auto/Deep)", "shipped", "Real model/corpus-backed reasoning"),
        Feature("Search scopes (Internal/Public/Both)", "shipped", "Corpus retrieval, not hardcoded arrays"),
        Feature("Speech-to-text", "shipped", "Browser Web Speech API + MAUI native STT"),
        Feature("Text-to-speech", "shipped", "speechSynthesis + MAUI TTS"),
        Feature("Conversation memory", "shipped", "Session, JSONL, and SQLite backends"),
        Feature("Conversation export (Markdown/JSON)", "shipped", "GET /api/memory/{sessionId}/export for shareable transcripts"),
        Feature("Document workspace", "shipped", "Upload, list, download, folder CRUD"),
        Feature("Image generation", "shipped", "Stable Diffusion optional + offline SVG renderer"),
        Feature("Video generation", "shipped", "Storyboard frames + ffmpeg stitch when available"),
        Feature("OAuth2/OIDC authentication", "shipped", "JWT bearer + cookie/OIDC when Auth:Enabled"),
        Feature("Freemium monetization", "shipped", "SQLite-backed plans, quotas, model gating"),
        Feature("FCA ecosystem entitlements", "shipped", "Link Auricrux account to FCA when configured"),
        Feature("Construction specialist corpus", "shipped", $"{corpusEntries} grounded entries across CSI/OSHA/PM/billing/code"),
        Feature("Live web browsing", "shipped", "POST /api/browse fetches http(s) URL text (SSRF-guarded) and LLM-summarizes for construction Q&A"),
        Feature("Agentic tool-use / plugins", "shipped", "POST /api/agent bounded tool loop: corpus_search, web_browse, construction calc tools"),
        Feature("Native code interpreter", "shipped", "POST /api/calc deterministic construction calculator (volume/rebar/BF/percent/units) — not sandboxed Python"),
        Feature("Vision / photo analysis", "shipped", "POST /api/vision construction field-photo intake + RFI draft; Ollama VisionModel optional for pixels"),
        Feature("Fine-tuned construction weights live", weights.FeatureStatus, weights.FeatureDetail)
    ];

    private static List<CompetitiveMatrixRow> BuildCompetitiveMatrix(WeightHonesty weights) =>
    [
        Matrix("Multi-model chat", "shipped", yes, yes, yes, yes, yes,
            "All major players offer model selection; Auricrux gates premium models behind freemium tiers."),
        Matrix("Construction specialist corpus", "shipped", no, no, no, no, no,
            "Auricrux moat: grounded internal/public construction knowledge base with scoped search — not generic web training."),
        Matrix("Thinking / reasoning modes", "shipped", yes, yes, yes, partial, yes,
            "Quick/Auto/Deep modes backed by real model calls; Copilot depth varies by surface."),
        Matrix("Scoped knowledge search", "shipped", partial, partial, partial, partial, partial,
            "Peers rely on RAG/connectors; Auricrux ships Internal/Public/Both corpus scopes out of the box."),
        Matrix("Speech-to-text", "shipped", yes, yes, yes, partial, yes,
            "Browser + MAUI native STT; Copilot mobile STT is surface-dependent."),
        Matrix("Text-to-speech", "shipped", yes, yes, yes, yes, partial,
            "Auto-speak after chat on web and mobile."),
        Matrix("Conversation memory + export", "shipped", yes, yes, yes, yes, partial,
            "Three persistence backends plus Markdown/JSON export for shareable transcripts."),
        Matrix("Document workspace", "shipped", partial, partial, partial, yes, no,
            "Upload/list/download folders; not a full cloud drive like M365 integration."),
        Matrix("Image generation", "shipped", yes, partial, yes, yes, partial,
            "Local SD optional + offline construction SVG renderer when SD unavailable."),
        Matrix("Video generation", "shipped", partial, no, partial, partial, no,
            "Storyboard frame pack + ffmpeg stitch; not generative video like Sora/Veo."),
        Matrix("Enterprise OAuth/OIDC", "shipped", yes, yes, yes, yes, partial,
            "JWT + cookie/OIDC when Auth:Enabled; dev mode stays open by default."),
        Matrix("Freemium monetization", "shipped", yes, yes, yes, partial, partial,
            "SQLite-backed plans, daily quotas, and model allow-lists enforced server-side."),
        Matrix("Agentic plugins / tool-use", "shipped", yes, yes, yes, yes, partial,
            "Bounded POST /api/agent tool loop (corpus/browse/calc) — not a third-party plugin marketplace."),
        Matrix("Code interpreter", "shipped", yes, partial, partial, partial, no,
            "Deterministic construction calculator (/api/calc); not a sandboxed Python notebook like ChatGPT."),
        Matrix("Live web browsing", "shipped", yes, partial, yes, partial, yes,
            "POST /api/browse: SSRF-guarded URL fetch + LLM construction summarize (not an autonomous agent browser)."),
        Matrix("Vision / photo analysis", "shipped", yes, yes, yes, partial, partial,
            "POST /api/vision: field-photo intake + OSHA/quality/RFI checklist + draft RFI; pixel vision when Auricrux:VisionModel set."),
        Matrix("Construction fine-tuned weights", weights.MatrixAuricrux, no, no, no, no, no,
            weights.MatrixNotes)
    ];

    private readonly record struct WeightHonesty(
        bool FineTuneLive,
        string FeatureStatus,
        string MatrixAuricrux,
        string EvalSuiteLastResult,
        string Notes,
        string OverallAssessment,
        string FeatureDetail,
        string MatrixNotes);

    private static CapabilityFeature Feature(string name, string status, string detail) =>
        new() { Name = name, Status = status, Detail = detail };

    private static CompetitiveMatrixRow Matrix(
        string feature,
        string auricrux,
        string chatGpt,
        string claude,
        string gemini,
        string copilot,
        string grok,
        string notes) =>
        new()
        {
            Feature = feature,
            Auricrux = auricrux,
            Peers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ChatGPT"] = chatGpt,
                ["Claude"] = claude,
                ["Gemini"] = gemini,
                ["Copilot"] = copilot,
                ["Grok"] = grok
            },
            Notes = notes
        };

    private const string yes = "yes";
    private const string partial = "partial";
    private const string no = "no";
}

public sealed class CapabilitiesReport
{
    public string App { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string PrimaryModel { get; set; } = string.Empty;
    public int CorpusEntries { get; set; }
    public CorpusStatsSnapshot CorpusStats { get; set; } = new();
    public ConstructionMoatSummary ConstructionMoat { get; set; } = new();
    public IReadOnlyList<string> Platforms { get; set; } = [];
    public IReadOnlyList<CapabilityFeature> Features { get; set; } = [];
    public IReadOnlyList<string> Competitors { get; set; } = [];
    public IReadOnlyList<CompetitiveMatrixRow> CompetitiveMatrix { get; set; } = [];
    public ParityScoreSummary ParityScore { get; set; } = new();
}

public sealed class CompetitiveMatrixRow
{
    public string Feature { get; set; } = string.Empty;
    public string Auricrux { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> Peers { get; set; } = new Dictionary<string, string>();
    public string Notes { get; set; } = string.Empty;
}

public sealed class ConstructionMoatSummary
{
    public string SpecialistModel { get; set; } = string.Empty;
    public bool CorpusGroundedSearch { get; set; }
    public string EvalSuite { get; set; } = string.Empty;
    public string EvalSuiteLastResult { get; set; } = string.Empty;
    public bool PromotedFineTuneLive { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class CapabilityFeature
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class ParityScoreSummary
{
    public int ShippedCore { get; set; }
    public int Planned { get; set; }
    public int Blocked { get; set; }
    public int MatrixRows { get; set; }
    public int AuricruxUniqueAdvantages { get; set; }
    public string OverallAssessment { get; set; } = string.Empty;
}

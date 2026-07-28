namespace Auricrux.Web.Services;

/// <summary>
/// Competitive feature matrix for major-player parity tracking (AUX-001 / AUX-002).
/// Each row is a capability area with honest shipped vs. planned status and per-peer comparison.
/// </summary>
public sealed class CapabilitiesService(ConstructionIntelligenceService intelligence, IConfiguration config)
{
    private static readonly string[] CompetitorNames = ["ChatGPT", "Claude", "Gemini", "Copilot", "Grok"];

    public CapabilitiesReport GetReport()
    {
        var primaryModel = config["Auricrux:PrimaryModel"] ?? "auricrux-fca";
        var corpusStats = intelligence.GetCorpusStats();
        var features = BuildFeatureList(corpusStats.TotalEntries);
        var matrix = BuildCompetitiveMatrix();
        var shipped = features.Count(f => f.Status == "shipped");
        var planned = features.Count(f => f.Status == "planned");
        var blocked = features.Count(f => f.Status == "blocked");

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
                EvalSuiteLastResult = "30/30 (100%)",
                PromotedFineTuneLive = false,
                Notes = "Runtime serves auricrux-fca Ollama alias with construction system prompt; checkpoint-70000 fine-tune not yet exported."
            },
            Platforms = ["Web (Blazor Server)", "Android (MAUI)", "Windows (MAUI)", "iOS (MAUI, macOS host)", "macOS (Mac Catalyst, macOS host)"],
            Features = features,
            Competitors = CompetitorNames,
            CompetitiveMatrix = matrix,
            ParityScore = new ParityScoreSummary
            {
                ShippedCore = shipped,
                Planned = planned,
                Blocked = blocked,
                MatrixRows = matrix.Count,
                AuricruxUniqueAdvantages = matrix.Count(r => r.Auricrux == "shipped" && r.Peers.Values.All(p => p is "no" or "partial")),
                OverallAssessment =
                    "PARTIAL — core chat/search/thinking/voice/workspace/media/auth/freemium/construction corpus are real; " +
                    "agentic tools, vision, web browse, and promoted fine-tune weights remain gaps vs. major players."
            }
        };
    }

    private static List<CapabilityFeature> BuildFeatureList(int corpusEntries) =>
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
        Feature("Agentic tool-use / plugins", "planned", "Not yet implemented"),
        Feature("Native code interpreter", "planned", "Not yet implemented"),
        Feature("Real-time web browsing", "planned", "Public search uses corpus, not live web crawl"),
        Feature("Vision / photo analysis", "planned", "Not yet implemented"),
        Feature("Fine-tuned construction weights live", "blocked", "checkpoint-70000 awaiting safe export (AUX-017/018)")
    ];

    private static List<CompetitiveMatrixRow> BuildCompetitiveMatrix() =>
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
        Matrix("Agentic plugins / tool-use", "planned", yes, yes, yes, yes, partial,
            "Major gap: no plugin runtime or autonomous tool orchestration yet."),
        Matrix("Code interpreter", "planned", yes, partial, partial, partial, no,
            "Not implemented; peers offer sandboxed Python or spreadsheet analysis."),
        Matrix("Live web browsing", "planned", yes, partial, yes, partial, yes,
            "Public search scope uses corpus hits, not live crawl — honest gap."),
        Matrix("Vision / photo analysis", "planned", yes, yes, yes, partial, partial,
            "Not implemented; critical for field photo RFI workflows."),
        Matrix("Construction fine-tuned weights", "blocked", no, no, no, no, no,
            "checkpoint-70000 not exported; auricrux-fca is system-prompt alias over llama3.2-class base (AUX-017 FAIL).")
    ];

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

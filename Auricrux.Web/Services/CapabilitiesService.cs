namespace Auricrux.Web.Services;

/// <summary>
/// Competitive feature matrix for major-player parity tracking (AUX-001 / AUX-002).
/// Each row is a capability area with honest shipped vs. planned status.
/// </summary>
public sealed class CapabilitiesService(ConstructionIntelligenceService intelligence, IConfiguration config)
{
    public CapabilitiesReport GetReport()
    {
        var primaryModel = config["Auricrux:PrimaryModel"] ?? "auricrux-fca";
        return new CapabilitiesReport
        {
            App = "Auricrux",
            Version = "1.1.0",
            PrimaryModel = primaryModel,
            CorpusEntries = intelligence.CorpusEntryCount,
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
            Features =
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
                Feature("Construction specialist corpus", "shipped", $"{intelligence.CorpusEntryCount} grounded entries across CSI/OSHA/PM"),
                Feature("Agentic tool-use / plugins", "planned", "Not yet implemented"),
                Feature("Native code interpreter", "planned", "Not yet implemented"),
                Feature("Real-time web browsing", "planned", "Public search uses corpus, not live web crawl"),
                Feature("Vision / photo analysis", "planned", "Not yet implemented"),
                Feature("Fine-tuned construction weights live", "blocked", "checkpoint-70000 awaiting safe export (AUX-017/018)")
            ],
            Competitors = ["ChatGPT", "Claude", "Gemini", "Copilot", "Grok"],
            ParityScore = new ParityScoreSummary
            {
                ShippedCore = 14,
                Planned = 4,
                Blocked = 1,
                OverallAssessment = "PARTIAL — core chat/search/thinking/voice/workspace/media/auth/freemium are real; agentic tools, vision, web browse, and promoted fine-tune weights remain gaps vs. major players."
            }
        };
    }

    private static CapabilityFeature Feature(string name, string status, string detail) =>
        new() { Name = name, Status = status, Detail = detail };
}

public sealed class CapabilitiesReport
{
    public string App { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string PrimaryModel { get; set; } = string.Empty;
    public int CorpusEntries { get; set; }
    public ConstructionMoatSummary ConstructionMoat { get; set; } = new();
    public IReadOnlyList<string> Platforms { get; set; } = [];
    public IReadOnlyList<CapabilityFeature> Features { get; set; } = [];
    public IReadOnlyList<string> Competitors { get; set; } = [];
    public ParityScoreSummary ParityScore { get; set; } = new();
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
    public string OverallAssessment { get; set; } = string.Empty;
}

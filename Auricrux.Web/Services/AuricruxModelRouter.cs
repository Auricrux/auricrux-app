using Auricrux.Shared.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services;

/// <summary>
/// Staged intelligence model router.
///
/// Analyzes a prompt and routes to the appropriate Ollama model tier:
///
///   Tier 1 — Primary   (auricrux-fca, 3B specialist)
///     Fast, low-VRAM. Handles most construction Q&A and short tasks.
///
///   Tier 2 — Secondary (llama3.2 / configurable 3B base)
///     Fallback when Primary is unavailable; also used for simple factual lookups.
///
///   Tier 3 — Tertiary  (mistral 7B / configurable)
///     Complex multi-step reasoning, legal clauses, long document analysis,
///     deep schedule analysis, delay claim drafting.
///
///   Tier 4 — Extended  (llama3.1:70b or any 70B class model if loaded)
///     Highest capability for autonomous agent tasks, complex code generation,
///     full specification review. Only activated when explicitly available.
///
///   Vision — VisionModel (llava or similar)
///     Image input: blueprints, site photos, markup review.
///
/// Routing rules are loaded from Atlas model_routes collection when available,
/// falling back to built-in defaults. This means rules can be updated in Atlas
/// without a code deploy.
///
/// Config keys (appsettings / env):
///   Auricrux:PrimaryModel    (default: auricrux-fca)
///   Auricrux:SecondaryModel  (default: llama3.2)
///   Auricrux:TertiaryModel   (default: mistral)
///   Auricrux:ExtendedModel   (default: llama3.1:70b — only used if loaded)
///   Auricrux:VisionModel     (default: llava)
/// </summary>
public sealed class AuricruxModelRouter
{
    private readonly IConfiguration _config;
    private readonly AtlasService _atlas;
    private readonly ILogger<AuricruxModelRouter> _logger;
    private readonly ModelTierConfig _tiers;

    public AuricruxModelRouter(IConfiguration config, AtlasService atlas, ILogger<AuricruxModelRouter> logger)
    {
        _config = config;
        _atlas = atlas;
        _logger = logger;
        _tiers = new ModelTierConfig
        {
            Primary = config["Auricrux:PrimaryModel"] ?? "auricrux-fca",
            Secondary = config["Auricrux:SecondaryModel"] ?? "llama3.2",
            Tertiary = config["Auricrux:TertiaryModel"] ?? "mistral",
            Extended = config["Auricrux:ExtendedModel"] ?? "llama3.1:70b",
            Vision = config["Auricrux:VisionModel"] ?? "llava",
        };
    }

    public ModelTierConfig Tiers => _tiers;

    /// <summary>
    /// Select the most appropriate model for a prompt.
    /// Returns the Ollama model name.
    /// </summary>
    public async Task<ModelSelection> SelectAsync(
        string prompt,
        ThinkingMode thinkingMode,
        bool hasImageAttachment,
        string? clientRequestedModel,
        CancellationToken ct = default)
    {
        // 1. Explicit caller override always wins
        if (!string.IsNullOrWhiteSpace(clientRequestedModel))
        {
            return new ModelSelection(clientRequestedModel, ModelTier.ClientOverride, "caller-specified");
        }

        // 2. Vision path
        if (hasImageAttachment)
        {
            return new ModelSelection(_tiers.Vision, ModelTier.Vision, "image-attachment");
        }

        // 3. ThinkingMode hard-routing
        if (thinkingMode == ThinkingMode.Quick)
        {
            return new ModelSelection(_tiers.Primary, ModelTier.Primary, "quick-mode");
        }

        // 4. Score prompt complexity
        var complexity = ScoreComplexity(prompt);

        // 5. Check Atlas for custom routing rules (non-blocking)
        var atlasOverride = await TryAtlasRuleAsync(complexity, thinkingMode, ct);
        if (atlasOverride is not null)
        {
            return atlasOverride;
        }

        // 6. Built-in tier selection
        return complexity switch
        {
            >= 0.85 => new ModelSelection(_tiers.Extended, ModelTier.Extended, $"complexity={complexity:F2}"),
            >= 0.60 => new ModelSelection(_tiers.Tertiary, ModelTier.Tertiary, $"complexity={complexity:F2}"),
            >= 0.30 => new ModelSelection(_tiers.Primary, ModelTier.Primary, $"complexity={complexity:F2}"),
            _ => new ModelSelection(_tiers.Secondary, ModelTier.Secondary, $"complexity={complexity:F2}-simple"),
        };
    }

    // ── Complexity scoring ────────────────────────────────────────────────────

    private static double ScoreComplexity(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return 0.2;

        var p = prompt.ToLowerInvariant();
        double score = 0.0;

        // Token length signal
        var wordCount = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        score += wordCount switch { > 200 => 0.30, > 80 => 0.18, > 40 => 0.08, _ => 0.0 };

        // Deep reasoning keywords
        if (ContainsAny(p, "analyze", "analyse", "compare", "evaluate", "review all", "comprehensive",
                        "step by step", "step-by-step", "in detail", "explain why", "walk me through"))
            score += 0.22;

        // Legal / contract complexity
        if (ContainsAny(p, "delay claim", "liquidated damages", "force majeure", "indemnif",
                        "consequential damages", "cumulative impact", "differing site", "change order dispute",
                        "contract clause", "acceleration cost", "fragnets", "time impact analysis"))
            score += 0.35;

        // Code / specification depth
        if (ContainsAny(p, "ibc section", "osha 1926", "aci 318", "aisc 360", "astm", "ieee",
                        "nfpa 70", "nec article", "ashrae", "leed credit", "astm c150", "specification division"))
            score += 0.25;

        // Multi-part queries
        var questionMarks = prompt.Count(c => c == '?');
        if (questionMarks >= 3) score += 0.20;
        else if (questionMarks == 2) score += 0.10;

        // Estimation / cost analysis
        if (ContainsAny(p, "cost estimate", "quantity takeoff", "unit price", "rsmeans",
                        "parametric estimate", "bid breakdown", "cost per sf", "ffe budget"))
            score += 0.15;

        // Schedule / CPM
        if (ContainsAny(p, "critical path", "float analysis", "earned value", "s-curve",
                        "schedule compression", "resource leveling", "baseline comparison"))
            score += 0.20;

        // Autonomous / multi-step agent tasks
        if (ContainsAny(p, "create a plan", "generate a report", "write a full", "draft the",
                        "build me a", "produce a complete", "outline all steps", "architect the"))
            score += 0.25;

        return Math.Clamp(score, 0.0, 1.0);
    }

    private static bool ContainsAny(string haystack, params string[] needles)
        => needles.Any(n => haystack.Contains(n, StringComparison.Ordinal));

    // ── Atlas routing rules ───────────────────────────────────────────────────

    private async Task<ModelSelection?> TryAtlasRuleAsync(double complexity, ThinkingMode mode, CancellationToken ct)
    {
        if (!_atlas.IsConfigured) return null;
        try
        {
            // Atlas model_routes document schema:
            // { _id, name, min_complexity, max_complexity, thinking_modes, model, tier, reason }
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Lte("min_complexity", complexity),
                Builders<BsonDocument>.Filter.Gt("max_complexity", complexity),
                Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Exists("thinking_modes", false),
                    Builders<BsonDocument>.Filter.AnyEq("thinking_modes", mode.ToString().ToLowerInvariant())
                )
            );

            using var cursor = await _atlas.ModelRoutes
                .Find(filter)
                .Sort(Builders<BsonDocument>.Sort.Descending("min_complexity"))
                .Limit(1)
                .ToCursorAsync(ct);

            if (await cursor.MoveNextAsync(ct) && cursor.Current.FirstOrDefault() is BsonDocument doc)
            {
                var model = doc.GetValue("model", BsonNull.Value).IsBsonNull ? null : doc["model"].AsString;
                var tier = doc.Contains("tier") ? Enum.TryParse<ModelTier>(doc["tier"].AsString, true, out var t) ? t : ModelTier.Primary : ModelTier.Primary;
                var reason = doc.Contains("reason") ? doc["reason"].AsString : "atlas-rule";
                if (!string.IsNullOrWhiteSpace(model))
                {
                    _logger.LogDebug("Atlas model route matched: model={Model} complexity={C:F2}", model, complexity);
                    return new ModelSelection(model, tier, $"atlas-rule:{reason}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Atlas model route lookup failed — using built-in tiers");
        }
        return null;
    }
}

// ── Value objects ──────────────────────────────────────────────────────────────

public sealed record ModelTierConfig
{
    public string Primary { get; init; } = "auricrux-fca";
    public string Secondary { get; init; } = "llama3.2";
    public string Tertiary { get; init; } = "mistral";
    public string Extended { get; init; } = "llama3.1:70b";
    public string Vision { get; init; } = "llava";

    public IReadOnlyList<string> All => [Primary, Secondary, Tertiary, Extended, Vision];
}

public sealed record ModelSelection(string Model, ModelTier Tier, string Reason);

public enum ModelTier
{
    ClientOverride,
    Secondary,
    Primary,
    Tertiary,
    Extended,
    Vision,
}

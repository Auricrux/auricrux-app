using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services;

/// <summary>
/// Analyzes feedback and interactions to identify knowledge gaps in the construction corpus.
/// Low-rated interactions (stars ≤ 2) reveal areas where Auricrux is providing inadequate
/// guidance, insufficient sources, or incorrect information.
/// </summary>
public sealed class KnowledgeGapAnalysisService
{
    private readonly AtlasService _atlas;
    private readonly ILogger<KnowledgeGapAnalysisService> _logger;

    public KnowledgeGapAnalysisService(AtlasService atlas, ILogger<KnowledgeGapAnalysisService> logger)
    {
        _atlas = atlas;
        _logger = logger;
    }

    /// <summary>
    /// Identify knowledge gaps from low-rated interactions.
    /// Returns aggregated patterns showing query types, frequency, and common issues.
    /// </summary>
    public async Task<List<KnowledgeGap>> AnalyzeGapsAsync(
        DateTime? since = null,
        int minOccurrences = 2,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            _logger.LogWarning("Atlas not configured — knowledge gap analysis unavailable");
            return [];
        }

        try
        {
            var sinceDate = since ?? DateTime.UtcNow.AddDays(-30);

            // Aggregate low-rated feedback (stars ≤ 2) with interaction details
            var pipeline = new BsonDocument[]
            {
                // Match low-rated feedback
                new BsonDocument { ["$match"] = new BsonDocument
                {
                    ["stars"] = new BsonDocument { ["$lte"] = 2 },
                    ["created_at"] = new BsonDocument { ["$gte"] = sinceDate }
                }},
                // Lookup interaction details
                new BsonDocument { ["$lookup"] = new BsonDocument
                {
                    ["from"] = "interactions",
                    ["localField"] = "interaction_id",
                    ["foreignField"] = "interaction_id",
                    ["as"] = "interaction"
                }},
                // Unwind interaction array (should be single document)
                new BsonDocument { ["$unwind"] = new BsonDocument
                {
                    ["path"] = "$interaction",
                    ["preserveNullAndEmptyArrays"] = false
                }},
                // Group by query pattern (extract topic keywords)
                new BsonDocument { ["$group"] = new BsonDocument
                {
                    ["_id"] = "$interaction.query",
                    ["count"] = new BsonDocument { ["$sum"] = 1 },
                    ["avg_stars"] = new BsonDocument { ["$avg"] = "$stars" },
                    ["avg_confidence"] = new BsonDocument { ["$avg"] = "$interaction.confidence_score" },
                    ["avg_source_count"] = new BsonDocument { ["$avg"] = new BsonDocument
                    {
                        ["$size"] = "$interaction.sources"
                    }},
                    ["sample_comments"] = new BsonDocument { ["$push"] = "$comment" },
                    ["first_seen"] = new BsonDocument { ["$min"] = "$created_at" },
                    ["last_seen"] = new BsonDocument { ["$max"] = "$created_at" }
                }},
                // Filter by minimum occurrences
                new BsonDocument { ["$match"] = new BsonDocument
                {
                    ["count"] = new BsonDocument { ["$gte"] = minOccurrences }
                }},
                // Sort by frequency (most common gaps first)
                new BsonDocument { ["$sort"] = new BsonDocument { ["count"] = -1 } },
                // Limit to top 50 gaps
                new BsonDocument { ["$limit"] = 50 }
            };

            var cursor = await _atlas.Feedback.AggregateAsync<BsonDocument>(pipeline, cancellationToken: ct);
            var results = await cursor.ToListAsync(ct);

            return results.Select(doc => new KnowledgeGap
            {
                QueryPattern = doc["_id"].AsString,
                Occurrences = doc["count"].AsInt32,
                AverageRating = doc["avg_stars"].ToDouble(),
                AverageConfidence = doc["avg_confidence"].ToDouble(),
                AverageSourceCount = doc.Contains("avg_source_count") ? doc["avg_source_count"].ToDouble() : 0,
                SampleComments = doc["sample_comments"].AsBsonArray
                    .Where(c => !c.IsBsonNull && !string.IsNullOrWhiteSpace(c.AsString))
                    .Select(c => c.AsString)
                    .Take(5)
                    .ToList(),
                FirstSeen = doc["first_seen"].ToUniversalTime(),
                LastSeen = doc["last_seen"].ToUniversalTime(),
                Category = InferCategory(doc["_id"].AsString),
                Severity = CalculateSeverity(doc["count"].AsInt32, doc["avg_stars"].ToDouble())
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze knowledge gaps");
            return [];
        }
    }

    /// <summary>
    /// Get detailed gap information including all affected interactions.
    /// </summary>
    public async Task<KnowledgeGapDetail?> GetGapDetailAsync(
        string queryPattern,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return null;

        try
        {
            // Find all feedback for this query pattern
            var feedbackFilter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Lte("stars", 2),
                Builders<BsonDocument>.Filter.Exists("interaction_id")
            );

            var feedbackDocs = await _atlas.Feedback
                .Find(feedbackFilter)
                .ToListAsync(ct);

            var interactionIds = feedbackDocs
                .Select(f => f["interaction_id"].AsString)
                .ToList();

            if (interactionIds.Count == 0) return null;

            // Find corresponding interactions
            var interactionFilter = Builders<BsonDocument>.Filter.In("interaction_id", interactionIds);
            var interactionDocs = await _atlas.Interactions
                .Find(interactionFilter)
                .ToListAsync(ct);

            // Filter to matching query pattern
            var matchingInteractions = interactionDocs
                .Where(i => i["query"].AsString.Contains(queryPattern, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingInteractions.Count == 0) return null;

            return new KnowledgeGapDetail
            {
                QueryPattern = queryPattern,
                TotalOccurrences = matchingInteractions.Count,
                InteractionIds = matchingInteractions.Select(i => i["interaction_id"].AsString).ToList(),
                Queries = matchingInteractions.Select(i => i["query"].AsString).Distinct().ToList(),
                AverageConfidence = matchingInteractions.Average(i => i["confidence_score"].ToDouble()),
                SourceCounts = matchingInteractions.Select(i => i["sources"].AsBsonArray.Count).ToList(),
                Comments = feedbackDocs
                    .Where(f => f.Contains("comment") && !f["comment"].IsBsonNull)
                    .Select(f => f["comment"].AsString)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get gap detail for pattern: {Pattern}", queryPattern);
            return null;
        }
    }

    private static string InferCategory(string query)
    {
        var lower = query.ToLowerInvariant();

        if (lower.Contains("osha") || lower.Contains("safety") || lower.Contains("fall") || lower.Contains("scaffold"))
            return "safety";
        if (lower.Contains("estimate") || lower.Contains("takeoff") || lower.Contains("cost") || lower.Contains("rsmeans"))
            return "estimating";
        if (lower.Contains("schedule") || lower.Contains("cpm") || lower.Contains("delay") || lower.Contains("critical"))
            return "scheduling";
        if (lower.Contains("concrete") || lower.Contains("steel") || lower.Contains("hvac") || lower.Contains("division"))
            return "technical";
        if (lower.Contains("contract") || lower.Contains("aia") || lower.Contains("change order") || lower.Contains("retainage"))
            return "contracts";
        if (lower.Contains("ibc") || lower.Contains("code") || lower.Contains("ada") || lower.Contains("egress"))
            return "code";
        if (lower.Contains("cte") || lower.Contains("training") || lower.Contains("academy"))
            return "education";

        return "general";
    }

    private static string CalculateSeverity(int occurrences, double avgRating)
    {
        if (occurrences >= 10 && avgRating < 1.5) return "critical";
        if (occurrences >= 5 && avgRating < 2.0) return "high";
        if (occurrences >= 2 && avgRating < 2.5) return "medium";
        return "low";
    }
}

// ── Value objects ─────────────────────────────────────────────────────────────

public sealed class KnowledgeGap
{
    public required string QueryPattern { get; init; }
    public int Occurrences { get; init; }
    public double AverageRating { get; init; }
    public double AverageConfidence { get; init; }
    public double AverageSourceCount { get; init; }
    public List<string> SampleComments { get; init; } = [];
    public DateTime FirstSeen { get; init; }
    public DateTime LastSeen { get; init; }
    public string Category { get; init; } = "general";
    public string Severity { get; init; } = "low";
}

public sealed class KnowledgeGapDetail
{
    public required string QueryPattern { get; init; }
    public int TotalOccurrences { get; init; }
    public List<string> InteractionIds { get; init; } = [];
    public List<string> Queries { get; init; } = [];
    public double AverageConfidence { get; init; }
    public List<int> SourceCounts { get; init; } = [];
    public List<string> Comments { get; init; } = [];
}

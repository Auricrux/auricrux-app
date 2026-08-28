using Auricrux.Web.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services;

/// <summary>
/// Generates individualized learning recommendations based on user activity, knowledge gaps, and outcomes.
/// Recommends specific learning topics when users encounter knowledge gaps or field challenges.
/// Tracks engagement to measure recommendation effectiveness.
/// </summary>
public sealed class LearningRecommendationService
{
    private readonly AtlasService _atlas;
    private readonly KnowledgeGapAnalysisService _gapAnalysis;
    private readonly ConstructionEventService _eventService;
    private readonly ILogger<LearningRecommendationService> _logger;

    public LearningRecommendationService(
        AtlasService atlas,
        KnowledgeGapAnalysisService gapAnalysis,
        ConstructionEventService eventService,
        ILogger<LearningRecommendationService> logger)
    {
        _atlas = atlas;
        _gapAnalysis = gapAnalysis;
        _eventService = eventService;
        _logger = logger;
    }

    /// <summary>
    /// Get personalized learning recommendations for a user based on their knowledge gaps.
    /// </summary>
    public async Task<List<LearningRecommendation>> GetRecommendationsForUserAsync(
        string userId,
        int limit = 5,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            _logger.LogWarning("Atlas not configured — learning recommendations unavailable");
            return [];
        }

        try
        {
            // Get user's existing recommendations (not yet engaged)
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("user_id", userId),
                Builders<BsonDocument>.Filter.Eq("engagement_status", "pending")
            );

            var existingDocs = await _atlas.LearningRecommendations
                .Find(filter)
                .SortByDescending(d => d["priority"])
                .ThenByDescending(d => d["generated_at"])
                .Limit(limit)
                .ToListAsync(ct);

            if (existingDocs.Count >= limit)
            {
                // Return existing recommendations
                return existingDocs.Select(MapRecommendation).ToList();
            }

            // Generate new recommendations from user's interactions
            var userInteractions = await GetUserInteractionsAsync(userId, limit: 20, ct);
            var lowRatedInteractions = userInteractions
                .Where(i => i.Contains("feedback_stars") && i["feedback_stars"].AsInt32 <= 2)
                .ToList();

            if (lowRatedInteractions.Count == 0)
            {
                // No gaps identified yet
                return existingDocs.Select(MapRecommendation).ToList();
            }

            // Group by topic/category and generate recommendations
            var recommendations = await GenerateRecommendationsFromInteractionsAsync(
                userId,
                lowRatedInteractions,
                limit - existingDocs.Count,
                ct);

            return existingDocs.Select(MapRecommendation)
                .Concat(recommendations)
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recommendations for user: {UserId}", userId);
            return [];
        }
    }

    /// <summary>
    /// Get learning recommendations that address a specific knowledge gap.
    /// </summary>
    public async Task<List<LearningRecommendation>> GetRecommendationsForGapAsync(
        string queryPattern,
        string? category = null,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return [];
        }

        try
        {
            var filterBuilder = Builders<BsonDocument>.Filter;
            var filters = new List<FilterDefinition<BsonDocument>>
            {
                filterBuilder.Eq("source_gap_pattern", queryPattern)
            };

            if (!string.IsNullOrWhiteSpace(category))
            {
                filters.Add(filterBuilder.Eq("category", category));
            }

            var filter = filterBuilder.And(filters);

            var docs = await _atlas.LearningRecommendations
                .Find(filter)
                .SortByDescending(d => d["priority"])
                .Limit(5)
                .ToListAsync(ct);

            return docs.Select(MapRecommendation).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recommendations for gap: {Pattern}", queryPattern);
            return [];
        }
    }

    /// <summary>
    /// Track user engagement with a recommendation (viewed, started, completed).
    /// </summary>
    public async Task<bool> TrackRecommendationEngagementAsync(
        string recommendationId,
        string engagementStatus,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return false;
        }

        try
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", recommendationId);
            var update = Builders<BsonDocument>.Update
                .Set("engagement_status", engagementStatus)
                .Set("engaged_at", DateTime.UtcNow);

            var result = await _atlas.LearningRecommendations.UpdateOneAsync(filter, update, cancellationToken: ct);

            if (result.ModifiedCount > 0)
            {
                _logger.LogInformation("Tracked recommendation engagement: {RecommendationId} status={Status}",
                    recommendationId, engagementStatus);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track recommendation engagement: {RecommendationId}", recommendationId);
            return false;
        }
    }

    /// <summary>
    /// Auto-generate learning recommendation from a construction outcome.
    /// When field outcome reveals a knowledge gap, recommend relevant training.
    /// </summary>
    public async Task<LearningRecommendation?> GenerateRecommendationFromOutcomeAsync(
        string outcomeId,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return null;
        }

        try
        {
            // Get outcome details
            var outcomeDoc = await _atlas.ConstructionOutcomes
                .Find(d => d["_id"] == outcomeId)
                .FirstOrDefaultAsync(ct);

            if (outcomeDoc == null)
            {
                return null;
            }

            var status = outcomeDoc["status"].AsString;
            var outcomeType = outcomeDoc["outcome_type"].AsString;
            var description = outcomeDoc.GetValue("description", "").AsString;

            // Only generate recommendations for failures or issues
            if (status == "success" || status == "completed")
            {
                return null;
            }

            // Get associated event
            var eventId = outcomeDoc["event_id"].AsString;
            var events = await _eventService.QueryEventsAsync(interactionId: eventId, limit: 1, ct: ct);

            if (events.Count == 0)
            {
                return null;
            }

            var evt = events[0];

            // Infer learning topic from event and outcome
            var topic = InferLearningTopic(evt.EventType, evt.Phase, description, outcomeType);
            var rationale = BuildRationale(evt, outcomeDoc, status);

            // Create recommendation
            var recommendationId = $"rec_{Guid.NewGuid()}";
            var doc = new BsonDocument
            {
                ["_id"] = recommendationId,
                ["recommendation_id"] = recommendationId,
                ["user_id"] = evt.UserId ?? "",
                ["topic"] = topic,
                ["rationale"] = rationale,
                ["suggested_action"] = $"Review best practices for {topic}",
                ["priority"] = "high",
                ["source_outcome_id"] = outcomeId,
                ["source_event_id"] = eventId,
                ["category"] = InferCategory(topic),
                ["academy_link"] = BsonNull.Value, // Placeholder until Phase 8
                ["generated_at"] = DateTime.UtcNow,
                ["engagement_status"] = "pending"
            };

            await _atlas.LearningRecommendations.InsertOneAsync(doc, cancellationToken: ct);

            _logger.LogInformation("Generated learning recommendation from outcome: {OutcomeId} → {RecommendationId}",
                outcomeId, recommendationId);

            return MapRecommendation(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate recommendation from outcome: {OutcomeId}", outcomeId);
            return null;
        }
    }

    /// <summary>
    /// Get recommendation effectiveness report.
    /// Shows engagement rate and follow-through for recommendations.
    /// </summary>
    public async Task<RecommendationEffectivenessReport> GetEffectivenessReportAsync(
        DateTime? since = null,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return new RecommendationEffectivenessReport
            {
                TotalRecommendations = 0,
                EngagedRecommendations = 0,
                EngagementRate = 0
            };
        }

        try
        {
            var sinceDate = since ?? DateTime.UtcNow.AddDays(-30);
            var filter = Builders<BsonDocument>.Filter.Gte("generated_at", sinceDate);

            var allDocs = await _atlas.LearningRecommendations.Find(filter).ToListAsync(ct);

            var total = allDocs.Count;
            var engaged = allDocs.Count(d => d.GetValue("engagement_status", "pending").AsString != "pending");
            var engagementRate = total > 0 ? (double)engaged / total : 0;

            return new RecommendationEffectivenessReport
            {
                TotalRecommendations = total,
                EngagedRecommendations = engaged,
                EngagementRate = engagementRate,
                Since = sinceDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recommendation effectiveness report");
            return new RecommendationEffectivenessReport
            {
                TotalRecommendations = 0,
                EngagedRecommendations = 0,
                EngagementRate = 0
            };
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<List<BsonDocument>> GetUserInteractionsAsync(
        string userId,
        int limit,
        CancellationToken ct)
    {
        if (!_atlas.IsConfigured)
            return [];

        try
        {
            var filter = Builders<BsonDocument>.Filter.Eq("user_id", userId);
            return await _atlas.Interactions
                .Find(filter)
                .SortByDescending(d => d["created_at"])
                .Limit(limit)
                .ToListAsync(ct);
        }
        catch
        {
            return [];
        }
    }

    private async Task<List<LearningRecommendation>> GenerateRecommendationsFromInteractionsAsync(
        string userId,
        List<BsonDocument> lowRatedInteractions,
        int limit,
        CancellationToken ct)
    {
        var recommendations = new List<LearningRecommendation>();

        // Group by topic/category
        var byCategory = lowRatedInteractions
            .GroupBy(i => ExtractCategory(i["query"].AsString))
            .OrderByDescending(g => g.Count())
            .Take(limit);

        foreach (var group in byCategory)
        {
            var category = group.Key;
            var queries = group.Select(i => i["query"].AsString).ToList();
            var topic = InferLearningTopicFromQueries(queries, category);

            var recommendationId = $"rec_{Guid.NewGuid()}";
            var doc = new BsonDocument
            {
                ["_id"] = recommendationId,
                ["recommendation_id"] = recommendationId,
                ["user_id"] = userId,
                ["topic"] = topic,
                ["rationale"] = $"Based on {group.Count()} recent queries about {category}",
                ["suggested_action"] = $"Review fundamentals of {topic}",
                ["priority"] = group.Count() >= 3 ? "high" : "medium",
                ["source_interaction_ids"] = new BsonArray(group.Select(i => i["interaction_id"].AsString)),
                ["source_gap_pattern"] = queries.FirstOrDefault() ?? "",
                ["category"] = category,
                ["academy_link"] = BsonNull.Value,
                ["generated_at"] = DateTime.UtcNow,
                ["engagement_status"] = "pending"
            };

            await _atlas.LearningRecommendations.InsertOneAsync(doc, cancellationToken: ct);
            recommendations.Add(MapRecommendation(doc));
        }

        return recommendations;
    }

    private static string ExtractCategory(string query)
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
        if (lower.Contains("contract") || lower.Contains("aia") || lower.Contains("change order"))
            return "contracts";
        if (lower.Contains("ibc") || lower.Contains("code") || lower.Contains("ada"))
            return "code";

        return "general";
    }

    private static string InferLearningTopicFromQueries(List<string> queries, string category)
    {
        return category switch
        {
            "safety" => "Construction Safety Fundamentals",
            "estimating" => "Cost Estimating Best Practices",
            "scheduling" => "Project Scheduling Techniques",
            "technical" => "Construction Methods & Materials",
            "contracts" => "Construction Contract Management",
            "code" => "Building Codes & Regulations",
            _ => "Construction Management Essentials"
        };
    }

    private static string InferLearningTopic(string eventType, string? phase, string description, string outcomeType)
    {
        var category = ExtractCategory(description);
        return InferLearningTopicFromQueries(new List<string> { description }, category);
    }

    private static string InferCategory(string topic)
    {
        var lower = topic.ToLowerInvariant();

        if (lower.Contains("safety"))
            return "safety";
        if (lower.Contains("estimating") || lower.Contains("cost"))
            return "estimating";
        if (lower.Contains("scheduling") || lower.Contains("project"))
            return "scheduling";
        if (lower.Contains("methods") || lower.Contains("materials"))
            return "technical";
        if (lower.Contains("contract"))
            return "contracts";
        if (lower.Contains("code") || lower.Contains("regulations"))
            return "code";

        return "general";
    }

    private static string BuildRationale(
        Shared.Models.ConstructionEvent evt,
        BsonDocument outcomeDoc,
        string status)
    {
        return $"Based on {status} outcome for {evt.EventType} activity. Consider reviewing related best practices.";
    }

    private static LearningRecommendation MapRecommendation(BsonDocument doc)
    {
        return new LearningRecommendation
        {
            RecommendationId = doc["recommendation_id"].AsString,
            UserId = doc.GetValue("user_id", "").AsString,
            Topic = doc["topic"].AsString,
            Rationale = doc.GetValue("rationale", "").AsString,
            SuggestedAction = doc.GetValue("suggested_action", "").AsString,
            Priority = doc.GetValue("priority", "medium").AsString,
            Category = doc.GetValue("category", "general").AsString,
            AcademyLink = doc.Contains("academy_link") && !doc["academy_link"].IsBsonNull
                ? doc["academy_link"].AsString
                : null,
            GeneratedAt = doc["generated_at"].ToUniversalTime(),
            EngagementStatus = doc.GetValue("engagement_status", "pending").AsString,
            EngagedAt = doc.Contains("engaged_at") && !doc["engaged_at"].IsBsonNull
                ? doc["engaged_at"].ToUniversalTime()
                : null
        };
    }
}

// ── Value objects ──────────────────────────────────────────────────────────────

public sealed class LearningRecommendation
{
    public required string RecommendationId { get; init; }
    public string UserId { get; init; } = "";
    public required string Topic { get; init; }
    public string Rationale { get; init; } = "";
    public string SuggestedAction { get; init; } = "";
    public string Priority { get; init; } = "medium";
    public string Category { get; init; } = "general";
    public string? AcademyLink { get; init; }
    public DateTime GeneratedAt { get; init; }
    public string EngagementStatus { get; init; } = "pending";
    public DateTime? EngagedAt { get; init; }
}

public sealed class RecommendationEffectivenessReport
{
    public int TotalRecommendations { get; init; }
    public int EngagedRecommendations { get; init; }
    public double EngagementRate { get; init; }
    public DateTime? Since { get; init; }
}

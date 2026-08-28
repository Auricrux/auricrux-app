using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services;

/// <summary>
/// Traces provenance of corpus entries, gaps, and recommendations back to source interactions.
/// Enables "how did we learn this?" queries showing complete learning pipeline lineage.
/// Distinguishes between observation, inference, validated fact, and authoritative guidance.
/// </summary>
public sealed class ProvenanceService
{
    private readonly AtlasService _atlas;
    private readonly ILogger<ProvenanceService> _logger;

    public ProvenanceService(AtlasService atlas, ILogger<ProvenanceService> logger)
    {
        _atlas = atlas;
        _logger = logger;
    }

    /// <summary>
    /// Trace a corpus entry back to its original source interactions.
    /// Shows complete provenance: interaction → feedback → gap → proposal → corpus.
    /// </summary>
    public async Task<CorpusProvenance?> GetCorpusEntryProvenanceAsync(
        string entryId,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return null;
        }

        try
        {
            // Get corpus entry
            var entry = await _atlas.Corpus.Find(d => d["_id"] == entryId).FirstOrDefaultAsync(ct);
            if (entry == null)
            {
                return null;
            }

            var provenance = new CorpusProvenance
            {
                EntryId = entryId,
                Title = entry["title"].AsString,
                Status = entry["status"].AsString,
                Source = entry.GetValue("source", "unknown").AsString,
                ConfidenceLevel = DetermineConfidenceLevel(entry),
                TruthLevel = DetermineTruthLevel(entry)
            };

            // Get source interaction IDs
            if (entry.Contains("source_interaction_id") && !entry["source_interaction_id"].IsBsonNull)
            {
                provenance.SourceInteractionId = entry["source_interaction_id"].AsString;

                // Get interaction details
                var interaction = await _atlas.Interactions
                    .Find(d => d["interaction_id"] == provenance.SourceInteractionId)
                    .FirstOrDefaultAsync(ct);

                if (interaction != null)
                {
                    provenance.OriginalQuery = interaction["query"].AsString;
                    provenance.OriginalTimestamp = interaction["created_at"].ToUniversalTime();
                }
            }

            // Get feedback IDs
            if (entry.Contains("source_feedback_ids") && entry["source_feedback_ids"].IsBsonArray)
            {
                provenance.SourceFeedbackIds = entry["source_feedback_ids"]
                    .AsBsonArray
                    .Select(id => id.AsString)
                    .ToList();

                // Get feedback details
                var feedbackDocs = await _atlas.Feedback
                    .Find(Builders<BsonDocument>.Filter.In("feedback_id", provenance.SourceFeedbackIds))
                    .ToListAsync(ct);

                provenance.FeedbackCount = feedbackDocs.Count;
                provenance.AverageFeedbackRating = feedbackDocs.Count > 0
                    ? feedbackDocs.Average(f => f["stars"].AsInt32)
                    : 0;
            }

            // Get gap pattern
            if (entry.Contains("source_query_pattern") && !entry["source_query_pattern"].IsBsonNull)
            {
                provenance.GapPattern = entry["source_query_pattern"].AsString;
            }

            // Get proposal details
            if (entry.Contains("originally_proposed_by"))
            {
                provenance.ProposedBy = entry["originally_proposed_by"].AsString;
                provenance.ProposedAt = entry["originally_proposed_at"].ToUniversalTime();
            }

            // Get approval details
            if (entry.Contains("approved_by"))
            {
                provenance.ApprovedBy = entry["approved_by"].AsString;
                provenance.ApprovedAt = entry.Contains("approved_at")
                    ? entry["approved_at"].ToUniversalTime()
                    : null;
            }

            return provenance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get corpus provenance: {EntryId}", entryId);
            return null;
        }
    }

    /// <summary>
    /// Show all feedback contributing to a knowledge gap.
    /// </summary>
    public async Task<GapProvenance?> GetGapAnalysisProvenanceAsync(
        string gapPattern,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return null;
        }

        try
        {
            // Find all interactions matching this gap pattern
            var interactionFilter = Builders<BsonDocument>.Filter.Regex("query",
                new BsonRegularExpression(gapPattern, "i"));
            var interactions = await _atlas.Interactions
                .Find(interactionFilter)
                .ToListAsync(ct);

            if (interactions.Count == 0)
            {
                return null;
            }

            var interactionIds = interactions.Select(i => i["interaction_id"].AsString).ToList();

            // Find all feedback for these interactions
            var feedbackFilter = Builders<BsonDocument>.Filter.In("interaction_id", interactionIds);
            var feedbackDocs = await _atlas.Feedback
                .Find(feedbackFilter)
                .ToListAsync(ct);

            var lowRatedFeedback = feedbackDocs.Where(f => f["stars"].AsInt32 <= 2).ToList();

            return new GapProvenance
            {
                GapPattern = gapPattern,
                TotalInteractions = interactions.Count,
                InteractionIds = interactionIds,
                TotalFeedback = feedbackDocs.Count,
                LowRatedFeedback = lowRatedFeedback.Count,
                AverageRating = feedbackDocs.Count > 0
                    ? feedbackDocs.Average(f => f["stars"].AsInt32)
                    : 0,
                SampleQueries = interactions
                    .Select(i => i["query"].AsString)
                    .Distinct()
                    .Take(5)
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get gap provenance: {Pattern}", gapPattern);
            return null;
        }
    }

    /// <summary>
    /// Show events and outcomes leading to a learning recommendation.
    /// </summary>
    public async Task<RecommendationProvenance?> GetRecommendationProvenanceAsync(
        string recommendationId,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return null;
        }

        try
        {
            // Get recommendation
            var rec = await _atlas.LearningRecommendations
                .Find(d => d["_id"] == recommendationId)
                .FirstOrDefaultAsync(ct);

            if (rec == null)
            {
                return null;
            }

            var provenance = new RecommendationProvenance
            {
                RecommendationId = recommendationId,
                Topic = rec["topic"].AsString,
                Category = rec.GetValue("category", "general").AsString,
                Priority = rec.GetValue("priority", "medium").AsString,
                GeneratedAt = rec["generated_at"].ToUniversalTime()
            };

            // Get source interactions
            if (rec.Contains("source_interaction_ids") && rec["source_interaction_ids"].IsBsonArray)
            {
                provenance.SourceInteractionIds = rec["source_interaction_ids"]
                    .AsBsonArray
                    .Select(id => id.AsString)
                    .ToList();
            }

            // Get source gap pattern
            if (rec.Contains("source_gap_pattern"))
            {
                provenance.SourceGapPattern = rec["source_gap_pattern"].AsString;
            }

            // Get source outcome
            if (rec.Contains("source_outcome_id"))
            {
                provenance.SourceOutcomeId = rec["source_outcome_id"].AsString;

                // Get outcome details
                var outcome = await _atlas.ConstructionOutcomes
                    .Find(d => d["_id"] == provenance.SourceOutcomeId)
                    .FirstOrDefaultAsync(ct);

                if (outcome != null)
                {
                    provenance.OutcomeStatus = outcome["status"].AsString;
                    provenance.OutcomeType = outcome["outcome_type"].AsString;

                    // Get associated event
                    var eventId = outcome["event_id"].AsString;
                    var evt = await _atlas.ConstructionEvents
                        .Find(d => d["_id"] == eventId)
                        .FirstOrDefaultAsync(ct);

                    if (evt != null)
                    {
                        provenance.SourceEventId = eventId;
                        provenance.EventType = evt["event_type"].AsString;
                    }
                }
            }

            return provenance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recommendation provenance: {RecommendationId}", recommendationId);
            return null;
        }
    }

    /// <summary>
    /// Generate full provenance graph for a resource showing all dependencies.
    /// </summary>
    public async Task<ProvenanceGraph> GenerateProvenanceGraphAsync(
        string resourceType,
        string resourceId,
        CancellationToken ct = default)
    {
        var graph = new ProvenanceGraph
        {
            ResourceType = resourceType,
            ResourceId = resourceId,
            Nodes = [],
            Edges = []
        };

        try
        {
            switch (resourceType)
            {
                case "corpus":
                    await BuildCorpusGraphAsync(resourceId, graph, ct);
                    break;
                case "gap":
                    await BuildGapGraphAsync(resourceId, graph, ct);
                    break;
                case "recommendation":
                    await BuildRecommendationGraphAsync(resourceId, graph, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate provenance graph");
        }

        return graph;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task BuildCorpusGraphAsync(string entryId, ProvenanceGraph graph, CancellationToken ct)
    {
        var provenance = await GetCorpusEntryProvenanceAsync(entryId, ct);
        if (provenance == null)
            return;

        // Add nodes
        graph.Nodes.Add(new ProvenanceNode { Id = entryId, Type = "corpus", Label = provenance.Title });

        if (!string.IsNullOrWhiteSpace(provenance.SourceInteractionId))
        {
            graph.Nodes.Add(new ProvenanceNode
            {
                Id = provenance.SourceInteractionId,
                Type = "interaction",
                Label = provenance.OriginalQuery ?? "Query"
            });
            graph.Edges.Add(new ProvenanceEdge
            {
                From = provenance.SourceInteractionId,
                To = entryId,
                Label = "led to corpus"
            });
        }

        foreach (var feedbackId in provenance.SourceFeedbackIds)
        {
            graph.Nodes.Add(new ProvenanceNode { Id = feedbackId, Type = "feedback", Label = "User feedback" });
            graph.Edges.Add(new ProvenanceEdge
            {
                From = feedbackId,
                To = entryId,
                Label = "validated"
            });
        }
    }

    private async Task BuildGapGraphAsync(string gapPattern, ProvenanceGraph graph, CancellationToken ct)
    {
        var provenance = await GetGapAnalysisProvenanceAsync(gapPattern, ct);
        if (provenance == null)
            return;

        graph.Nodes.Add(new ProvenanceNode { Id = gapPattern, Type = "gap", Label = gapPattern });

        foreach (var interactionId in provenance.InteractionIds.Take(10))
        {
            graph.Nodes.Add(new ProvenanceNode { Id = interactionId, Type = "interaction", Label = "Query" });
            graph.Edges.Add(new ProvenanceEdge
            {
                From = interactionId,
                To = gapPattern,
                Label = "contributed to gap"
            });
        }
    }

    private async Task BuildRecommendationGraphAsync(string recommendationId, ProvenanceGraph graph, CancellationToken ct)
    {
        var provenance = await GetRecommendationProvenanceAsync(recommendationId, ct);
        if (provenance == null)
            return;

        graph.Nodes.Add(new ProvenanceNode
        {
            Id = recommendationId,
            Type = "recommendation",
            Label = provenance.Topic
        });

        if (!string.IsNullOrWhiteSpace(provenance.SourceEventId))
        {
            graph.Nodes.Add(new ProvenanceNode
            {
                Id = provenance.SourceEventId,
                Type = "event",
                Label = provenance.EventType ?? "Event"
            });
            graph.Edges.Add(new ProvenanceEdge
            {
                From = provenance.SourceEventId,
                To = recommendationId,
                Label = "triggered recommendation"
            });
        }
    }

    private static string DetermineConfidenceLevel(BsonDocument entry)
    {
        // Approved entries with validated sources are high confidence
        if (entry["status"].AsString == "approved" &&
            entry.Contains("validated_sources") &&
            entry["validated_sources"].AsBsonArray.Count > 0)
        {
            return "high";
        }

        // Proposed entries are medium confidence
        if (entry["status"].AsString == "proposed")
        {
            return "medium";
        }

        return "low";
    }

    private static string DetermineTruthLevel(BsonDocument entry)
    {
        var status = entry["status"].AsString;
        var source = entry.GetValue("source", "").AsString;

        // Approved entries with validation are validated facts
        if (status == "approved" && entry.Contains("validated_answer"))
        {
            return "validated_fact";
        }

        // Auto-generated proposals are inferences
        if (entry.Contains("auto_generated") && entry["auto_generated"].AsBoolean)
        {
            return "inference";
        }

        // Proposed entries are observations
        if (status == "proposed")
        {
            return "observation";
        }

        // Approved entries from field outcomes are authoritative
        if (status == "approved" && source.Contains("outcome"))
        {
            return "authoritative_guidance";
        }

        return "observation";
    }
}

// ── Value objects ──────────────────────────────────────────────────────────────

public sealed class CorpusProvenance
{
    public required string EntryId { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public required string Source { get; init; }
    public string? SourceInteractionId { get; init; }
    public List<string> SourceFeedbackIds { get; init; } = [];
    public string? GapPattern { get; init; }
    public string? OriginalQuery { get; init; }
    public DateTime? OriginalTimestamp { get; init; }
    public int FeedbackCount { get; init; }
    public double AverageFeedbackRating { get; init; }
    public string? ProposedBy { get; init; }
    public DateTime? ProposedAt { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string ConfidenceLevel { get; init; } = "low";
    public string TruthLevel { get; init; } = "observation";
}

public sealed class GapProvenance
{
    public required string GapPattern { get; init; }
    public int TotalInteractions { get; init; }
    public List<string> InteractionIds { get; init; } = [];
    public int TotalFeedback { get; init; }
    public int LowRatedFeedback { get; init; }
    public double AverageRating { get; init; }
    public List<string> SampleQueries { get; init; } = [];
}

public sealed class RecommendationProvenance
{
    public required string RecommendationId { get; init; }
    public required string Topic { get; init; }
    public required string Category { get; init; }
    public required string Priority { get; init; }
    public DateTime GeneratedAt { get; init; }
    public List<string> SourceInteractionIds { get; init; } = [];
    public string? SourceGapPattern { get; init; }
    public string? SourceOutcomeId { get; init; }
    public string? SourceEventId { get; init; }
    public string? EventType { get; init; }
    public string? OutcomeStatus { get; init; }
    public string? OutcomeType { get; init; }
}

public sealed class ProvenanceGraph
{
    public required string ResourceType { get; init; }
    public required string ResourceId { get; init; }
    public List<ProvenanceNode> Nodes { get; init; } = [];
    public List<ProvenanceEdge> Edges { get; init; } = [];
}

public sealed class ProvenanceNode
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Label { get; init; }
}

public sealed class ProvenanceEdge
{
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Label { get; init; }
}

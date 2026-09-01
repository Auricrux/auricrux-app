using Auricrux.Web.Services.Breakthrough;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services;

/// <summary>
/// Phase 9B: Intelligence Dashboard Service
/// Aggregates metrics and provides comprehensive observability into Auricrux's learning loop
/// </summary>
public class IntelligenceDashboardService
{
    private readonly AtlasService _atlas;
    private readonly PhysicalVerificationService _verification;
    private readonly MetaLearningService _metaLearning;
    private readonly BackendHealthService _backendHealth;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<IntelligenceDashboardService> _logger;

    public IntelligenceDashboardService(
        AtlasService atlas,
        PhysicalVerificationService verification,
        MetaLearningService metaLearning,
        BackendHealthService backendHealth,
        IHttpClientFactory http,
        IConfiguration config,
        ILogger<IntelligenceDashboardService> logger)
    {
        _atlas = atlas;
        _verification = verification;
        _metaLearning = metaLearning;
        _backendHealth = backendHealth;
        _http = http;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Breakthrough loop activity from the in-process verification cache.
    /// This is the one view that stays truthful without Atlas, because the
    /// self-correction loop runs in memory whether or not persistence is configured.
    /// </summary>
    public async Task<BreakthroughActivity> GetBreakthroughActivityAsync(
        TimeSpan period,
        CancellationToken ct = default)
    {
        var stats = await _verification.GetAccuracyStatsAsync(period, ct);

        var activity = new BreakthroughActivity
        {
            Period = FormatPeriod(period),
            Persistence = _atlas.IsConfigured ? "atlas" : "in-memory",
            VerificationsRecorded = stats.TotalVerifications,
            AverageAccuracy = stats.AverageAccuracy,
            MedianAccuracy = stats.MedianAccuracy,
            CorrectionRate = stats.CorrectionRate,
            MostCommonErrors = stats.MostCommonErrors
                .Select(e => new BreakthroughErrorCount { Error = e.Error, Count = e.Count })
                .ToList()
        };

        // Meta-learning needs a minimum sample before its patterns mean anything.
        if (stats.TotalVerifications >= 10)
        {
            var insight = await _metaLearning.AnalyzeModelErrorsAsync("auricrux-primary", period, ct);
            activity.SystematicErrorPatterns = insight.SystematicErrors.Count;
            activity.RecommendedExperiments = insight.RecommendedExperiments;
        }
        else
        {
            activity.RecommendedExperiments =
            [
                $"Record {10 - stats.TotalVerifications} more field verifications to unlock meta-learning"
            ];
        }

        return activity;
    }

    /// <summary>
    /// Get dashboard overview metrics for a time period
    /// </summary>
    public async Task<DashboardOverview> GetOverviewAsync(
        TimeSpan period,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            // Atlas-backed counters are genuinely unavailable, but the breakthrough loop
            // still runs in memory — report that rather than a misleading wall of zeros.
            var breakthrough = await GetBreakthroughActivityAsync(period, ct);

            return new DashboardOverview
            {
                Period = FormatPeriod(period),
                OutcomesVerified = breakthrough.VerificationsRecorded,
                Status = "in_memory_breakthrough",
                StatusMessage =
                    $"Atlas persistence is off. Self-correction loop ran {breakthrough.VerificationsRecorded} " +
                    $"verification(s) in memory at {breakthrough.AverageAccuracy:P0} average accuracy. " +
                    "Learning-loop counters that require Atlas are unavailable.",
                Health = await GetSystemHealthAsync(ct)
            };
        }

        var cutoff = DateTime.UtcNow.Subtract(period);

        try
        {
            // Parallel metric gathering for performance
            var eventCount = await CountEventsAsync(cutoff, ct);
            var outcomeCount = await CountOutcomesAsync(cutoff, ct);
            var verifiedCount = await CountVerifiedOutcomesAsync(cutoff, ct);
            var gapCount = await CountKnowledgeGapsAsync(cutoff, ct);
            var recommendationCount = await CountRecommendationsAsync(cutoff, ct);
            var transferCount = await CountPredictiveTransfersAsync(cutoff, ct);
            var preventedCount = await EstimateIssuesPreventedAsync(cutoff, ct);
            var cycleTime = await CalculateAverageCycleTimeAsync(cutoff, ct);

            var overview = new DashboardOverview
            {
                Period = FormatPeriod(period),
                EventsCaptured = (int)eventCount,
                OutcomesRecorded = (int)outcomeCount,
                OutcomesVerified = (int)verifiedCount,
                KnowledgeGapsIdentified = (int)gapCount,
                RecommendationsGenerated = (int)recommendationCount,
                PredictiveTransfers = (int)transferCount,
                IssuesPrevented = (int)preventedCount,
                EstimatedSavingsUsd = CalculateSavings((int)preventedCount),
                LearningCycleTimeMinutes = cycleTime,
                Health = await GetSystemHealthAsync(ct),
                Status = "operational"
            };

            return overview;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard overview");
            return new DashboardOverview { Status = "error", StatusMessage = ex.Message };
        }
    }

    /// <summary>
    /// Get learning loop stage metrics
    /// </summary>
    public async Task<LearningLoopMetrics> GetLearningLoopMetricsAsync(CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return new LearningLoopMetrics();

        try
        {
            var cutoff24h = DateTime.UtcNow.AddHours(-24);
            var cutoff7d = DateTime.UtcNow.AddDays(-7);

            var stages = new List<LearningLoopStage>
            {
                new LearningLoopStage
                {
                    Name = "Event Capture",
                    Count24h = await CountEventsAsync(cutoff24h, ct),
                    Count7d = await CountEventsAsync(cutoff7d, ct),
                    Status = "active"
                },
                new LearningLoopStage
                {
                    Name = "Contextualization",
                    Count24h = await CountContextualizedEventsAsync(cutoff24h, ct),
                    Count7d = await CountContextualizedEventsAsync(cutoff7d, ct),
                    Status = "active"
                },
                new LearningLoopStage
                {
                    Name = "Outcome Capture",
                    Count24h = await CountOutcomesAsync(cutoff24h, ct),
                    Count7d = await CountOutcomesAsync(cutoff7d, ct),
                    Status = "active"
                },
                new LearningLoopStage
                {
                    Name = "Validation",
                    Count24h = await CountVerifiedOutcomesAsync(cutoff24h, ct),
                    Count7d = await CountVerifiedOutcomesAsync(cutoff7d, ct),
                    Status = "active"
                },
                new LearningLoopStage
                {
                    Name = "Knowledge Extraction",
                    Count24h = await CountKnowledgeGapsAsync(cutoff24h, ct),
                    Count7d = await CountKnowledgeGapsAsync(cutoff7d, ct),
                    Status = "active"
                },
                new LearningLoopStage
                {
                    Name = "Individualized Guidance",
                    Count24h = await CountRecommendationsAsync(cutoff24h, ct),
                    Count7d = await CountRecommendationsAsync(cutoff7d, ct),
                    Status = "active"
                },
                new LearningLoopStage
                {
                    Name = "Predictive Transfer",
                    Count24h = await CountPredictiveTransfersAsync(cutoff24h, ct),
                    Count7d = await CountPredictiveTransfersAsync(cutoff7d, ct),
                    Status = "active"
                },
                new LearningLoopStage
                {
                    Name = "Workflow Improvement",
                    Count24h = await CountImprovementProposalsAsync(cutoff24h, ct),
                    Count7d = await CountImprovementProposalsAsync(cutoff7d, ct),
                    Status = "active"
                }
            };

            return new LearningLoopMetrics
            {
                Stages = stages,
                CurrentThroughput = stages.FirstOrDefault()?.Count24h / 24.0 ?? 0,
                BottleneckStage = IdentifyBottleneck(stages)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting learning loop metrics");
            return new LearningLoopMetrics();
        }
    }

    /// <summary>
    /// Get recent predictive intelligence transfers
    /// </summary>
    public async Task<List<PredictiveTransferSummary>> GetRecentTransfersAsync(
        int limit = 20,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return new List<PredictiveTransferSummary>();

        try
        {
            // Get recent audit trail entries for predictive transfers
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("action_type", "predictive_transfer"),
                Builders<BsonDocument>.Filter.Eq("result", "success")
            );

            var transfers = await _atlas.AuditTrail
                .Find(filter)
                .SortByDescending(x => x["timestamp"])
                .Limit(limit)
                .ToListAsync(ct);

            return transfers.Select(t => new PredictiveTransferSummary
            {
                TransferId = t.GetValue("action_id", "unknown").AsString,
                Timestamp = t.GetValue("timestamp", DateTime.UtcNow).ToUniversalTime(),
                SourceOutcome = GetStringFromDetails(t, "source_outcome"),
                SourceProject = GetStringFromDetails(t, "source_project"),
                TargetProject = t.GetValue("resource_id", "unknown").AsString,
                SimilarityScore = GetDoubleFromDetails(t, "similarity_score"),
                MatchedFactors = GetArrayFromDetails(t, "matched_factors"),
                PredictedTimeframe = GetStringFromDetails(t, "predicted_timeframe"),
                Status = "delivered"
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent transfers");
            return new List<PredictiveTransferSummary>();
        }
    }

    /// <summary>
    /// Get active knowledge gaps
    /// </summary>
    public async Task<List<KnowledgeGapSummary>> GetActiveKnowledgeGapsAsync(CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return new List<KnowledgeGapSummary>();

        try
        {
            var filter = Builders<BsonDocument>.Filter.Eq("status", "active");
            
            var gaps = await _atlas.KnowledgeGaps
                .Find(filter)
                .SortByDescending(x => x["frequency"])
                .Limit(50)
                .ToListAsync(ct);

            return gaps.Select(g => new KnowledgeGapSummary
            {
                GapId = g.GetValue("gap_id", g.GetValue("_id", "unknown").AsString).AsString,
                Pattern = g.GetValue("gap_pattern", g.GetValue("pattern", "unknown")).AsString,
                Frequency = g.GetValue("frequency", 0).AsInt32,
                Severity = g.GetValue("severity", "unknown").AsString,
                RecommendationsCount = g.GetValue("recommendations_count", 0).AsInt32,
                AcademyLinkStatus = g.GetValue("academy_link_status", "unknown").AsString,
                FirstIdentified = g.GetValue("first_identified", DateTime.UtcNow).ToUniversalTime()
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting knowledge gaps");
            return new List<KnowledgeGapSummary>();
        }
    }

    /// <summary>
    /// Get recent audit trail actions
    /// </summary>
    public async Task<List<AuditAction>> GetRecentAuditActionsAsync(
        int limit = 50,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return new List<AuditAction>();

        try
        {
            var actions = await _atlas.AuditTrail
                .Find(Builders<BsonDocument>.Filter.Empty)
                .SortByDescending(x => x["timestamp"])
                .Limit(limit)
                .ToListAsync(ct);

            return actions.Select(a => new AuditAction
            {
                Timestamp = a.GetValue("timestamp", DateTime.UtcNow).ToUniversalTime(),
                Actor = a.GetValue("actor_id", "unknown").AsString,
                ActionType = a.GetValue("action_type", "unknown").AsString,
                ResourceType = a.GetValue("resource_type", "unknown").AsString,
                ResourceId = a.GetValue("resource_id", "").AsString,
                Description = FormatAuditDescription(a),
                Result = a.GetValue("result", "unknown").AsString
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit actions");
            return new List<AuditAction>();
        }
    }

    // ── Private Helper Methods ────────────────────────────────────────────────────

    private async Task<long> CountEventsAsync(DateTime cutoff, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Gte("timestamp", cutoff);
        return await _atlas.ConstructionEvents.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    private async Task<long> CountContextualizedEventsAsync(DateTime cutoff, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Gte("timestamp", cutoff),
            Builders<BsonDocument>.Filter.Ne("user_id", ""),
            Builders<BsonDocument>.Filter.Ne("project_id", "")
        );
        return await _atlas.ConstructionEvents.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    private async Task<long> CountOutcomesAsync(DateTime cutoff, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Gte("recorded_at", cutoff);
        return await _atlas.ConstructionOutcomes.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    private async Task<long> CountVerifiedOutcomesAsync(DateTime cutoff, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Gte("recorded_at", cutoff),
            Builders<BsonDocument>.Filter.Eq("validation_status", "verified")
        );
        return await _atlas.ConstructionOutcomes.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    private async Task<long> CountKnowledgeGapsAsync(DateTime cutoff, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Gte("first_identified", cutoff);
        return await _atlas.KnowledgeGaps.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    private async Task<long> CountRecommendationsAsync(DateTime cutoff, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Gte("generated_at", cutoff);
        return await _atlas.LearningRecommendations.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    private async Task<long> CountPredictiveTransfersAsync(DateTime cutoff, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("type", "predictive"),
            Builders<BsonDocument>.Filter.Gte("generated_at", cutoff)
        );
        return await _atlas.LearningRecommendations.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    private async Task<long> CountImprovementProposalsAsync(DateTime cutoff, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("status", "proposed"),
            Builders<BsonDocument>.Filter.Gte("proposed_at", cutoff)
        );
        return await _atlas.ImprovementProposals.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    private async Task<long> EstimateIssuesPreventedAsync(DateTime cutoff, CancellationToken ct)
    {
        // Count predictive recommendations that were marked as "acted upon"
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("type", "predictive"),
            Builders<BsonDocument>.Filter.Eq("engagement_status", "acted_upon"),
            Builders<BsonDocument>.Filter.Gte("generated_at", cutoff)
        );
        return await _atlas.LearningRecommendations.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    private Task<double> CalculateAverageCycleTimeAsync(DateTime cutoff, CancellationToken ct)
    {
        // Event→recommendation timestamps are not in the current schema.
        _ = cutoff;
        _ = ct;
        return Task.FromResult(0.0);
    }

    private long CalculateSavings(int issuesPrevented)
    {
        // Average cost per construction issue: ~$15,000 (delays, rework, etc.)
        return issuesPrevented * 15000;
    }

    private async Task<SystemHealth> GetSystemHealthAsync(CancellationToken ct)
    {
        var backend = await _backendHealth.ProbeAsync(ct);
        var ollama = !backend.OllamaReachable
            ? "unreachable"
            : backend.PrimaryModelReady ? "healthy" : "degraded";

        return new SystemHealth
        {
            AtlasStatus = _atlas.IsConfigured ? "configured" : "unavailable",
            FcaApiStatus = await ProbeFcaApiAsync(ct),
            OllamaStatus = ollama,
            RuntimeMode = backend.RuntimeMode,
            WorkerStatus = "unobserved",
            OrchestratorStatus = "hosted"
        };
    }

    private async Task<string> ProbeFcaApiAsync(CancellationToken ct)
    {
        var baseUrl = _config["FcaEcosystem:ApiBaseUrl"]
                      ?? _config["Auricrux:FcaEcosystemApiBase"];
        if (string.IsNullOrWhiteSpace(baseUrl)) return "not_configured";

        try
        {
            var http = _http.CreateClient("FcaEcosystem");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            var root = baseUrl.TrimEnd('/') + "/";
            using var response = await http.GetAsync(new Uri(new Uri(root), "health"), timeout.Token);
            return response.IsSuccessStatusCode ? "healthy" : $"http_{(int)response.StatusCode}";
        }
        catch
        {
            return "unreachable";
        }
    }

    private string FormatPeriod(TimeSpan period)
    {
        if (period.TotalHours <= 24) return "24h";
        if (period.TotalDays <= 7) return "7d";
        if (period.TotalDays <= 30) return "30d";
        return $"{(int)period.TotalDays}d";
    }

    private string? IdentifyBottleneck(List<LearningLoopStage> stages)
    {
        // Find stage with lowest throughput relative to previous stage
        for (int i = 1; i < stages.Count; i++)
        {
            var prev = stages[i - 1];
            var current = stages[i];
            
            if (prev.Count24h > 0 && current.Count24h < prev.Count24h * 0.5)
            {
                return current.Name;
            }
        }
        return null;
    }

    private string GetStringFromDetails(BsonDocument doc, string key)
    {
        try
        {
            var details = doc.GetValue("details", BsonNull.Value);
            if (details.IsBsonDocument)
            {
                return details.AsBsonDocument.GetValue(key, "").AsString;
            }
        }
        catch { }
        return "";
    }

    private double GetDoubleFromDetails(BsonDocument doc, string key)
    {
        try
        {
            var details = doc.GetValue("details", BsonNull.Value);
            if (details.IsBsonDocument)
            {
                return details.AsBsonDocument.GetValue(key, 0.0).ToDouble();
            }
        }
        catch { }
        return 0.0;
    }

    private List<string> GetArrayFromDetails(BsonDocument doc, string key)
    {
        try
        {
            var details = doc.GetValue("details", BsonNull.Value);
            if (details.IsBsonDocument)
            {
                var arr = details.AsBsonDocument.GetValue(key, new BsonArray());
                if (arr.IsBsonArray)
                {
                    return arr.AsBsonArray.Select(x => x.AsString).ToList();
                }
            }
        }
        catch { }
        return new List<string>();
    }

    private string FormatAuditDescription(BsonDocument action)
    {
        var actionType = action.GetValue("action_type", "").AsString;
        var resourceType = action.GetValue("resource_type", "").AsString;
        var resourceId = action.GetValue("resource_id", "").AsString;

        return actionType switch
        {
            "predictive_transfer" => $"Predictive intelligence transfer to {resourceType} {resourceId}",
            "outcome_verified" => $"Outcome {resourceId} verified",
            "recommendation_generated" => $"Recommendation generated for {resourceType} {resourceId}",
            "gap_identified" => $"Knowledge gap identified: {resourceId}",
            _ => $"{actionType} on {resourceType} {resourceId}"
        };
    }
}

// ── Data Transfer Objects ────────────────────────────────────────────────────

public class DashboardOverview
{
    public string Period { get; set; } = "";
    public int EventsCaptured { get; set; }
    public int OutcomesRecorded { get; set; }
    public int OutcomesVerified { get; set; }
    public int KnowledgeGapsIdentified { get; set; }
    public int RecommendationsGenerated { get; set; }
    public int PredictiveTransfers { get; set; }
    public int IssuesPrevented { get; set; }
    public long EstimatedSavingsUsd { get; set; }
    public double LearningCycleTimeMinutes { get; set; }
    public SystemHealth Health { get; set; } = new();
    public string Status { get; set; } = "";
    public string StatusMessage { get; set; } = "";
}

public class BreakthroughActivity
{
    public string Period { get; set; } = "";
    public string Persistence { get; set; } = "";
    public int VerificationsRecorded { get; set; }
    public double AverageAccuracy { get; set; }
    public double MedianAccuracy { get; set; }
    public double CorrectionRate { get; set; }
    public int SystematicErrorPatterns { get; set; }
    public List<string> RecommendedExperiments { get; set; } = new();
    public List<BreakthroughErrorCount> MostCommonErrors { get; set; } = new();
}

public class BreakthroughErrorCount
{
    public string Error { get; set; } = "";
    public int Count { get; set; }
}

public class SystemHealth
{
    public string AtlasStatus { get; set; } = "";
    public string FcaApiStatus { get; set; } = "";
    public string OllamaStatus { get; set; } = "";
    public string RuntimeMode { get; set; } = "";
    public string WorkerStatus { get; set; } = "";
    public string OrchestratorStatus { get; set; } = "";
}

public class LearningLoopMetrics
{
    public List<LearningLoopStage> Stages { get; set; } = new();
    public double CurrentThroughput { get; set; }
    public string? BottleneckStage { get; set; }
}

public class LearningLoopStage
{
    public string Name { get; set; } = "";
    public long Count24h { get; set; }
    public long Count7d { get; set; }
    public double AvgPerHour => Count24h / 24.0;
    public string Status { get; set; } = "";
}

public class PredictiveTransferSummary
{
    public string TransferId { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string SourceOutcome { get; set; } = "";
    public string SourceProject { get; set; } = "";
    public string TargetProject { get; set; } = "";
    public double SimilarityScore { get; set; }
    public List<string> MatchedFactors { get; set; } = new();
    public string PredictedTimeframe { get; set; } = "";
    public string Status { get; set; } = "";
}

public class KnowledgeGapSummary
{
    public string GapId { get; set; } = "";
    public string Pattern { get; set; } = "";
    public int Frequency { get; set; }
    public string Severity { get; set; } = "";
    public int RecommendationsCount { get; set; }
    public string AcademyLinkStatus { get; set; } = "";
    public DateTime FirstIdentified { get; set; }
}

public class AuditAction
{
    public DateTime Timestamp { get; set; }
    public string Actor { get; set; } = "";
    public string ActionType { get; set; } = "";
    public string ResourceType { get; set; } = "";
    public string ResourceId { get; set; } = "";
    public string Description { get; set; } = "";
    public string Result { get; set; } = "";
}

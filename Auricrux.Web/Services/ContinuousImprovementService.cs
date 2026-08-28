using Auricrux.Web.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services;

/// <summary>
/// Automates the learning pipeline with weekly analysis, auto-proposal generation, and quality trend tracking.
/// Runs scheduled analyses to measure learning loop health and auto-generate high-confidence corpus proposals.
/// </summary>
public sealed class ContinuousImprovementService
{
    private readonly AtlasService _atlas;
    private readonly KnowledgeGapAnalysisService _gapAnalysis;
    private readonly CorpusImprovementService _corpusImprovement;
    private readonly ILogger<ContinuousImprovementService> _logger;

    public ContinuousImprovementService(
        AtlasService atlas,
        KnowledgeGapAnalysisService gapAnalysis,
        CorpusImprovementService corpusImprovement,
        ILogger<ContinuousImprovementService> logger)
    {
        _atlas = atlas;
        _gapAnalysis = gapAnalysis;
        _corpusImprovement = corpusImprovement;
        _logger = logger;
    }

    /// <summary>
    /// Run complete weekly analysis of learning pipeline metrics.
    /// Aggregates interactions, feedback, gaps, proposals, and recommendations.
    /// </summary>
    public async Task<WeeklyAnalysisReport> RunWeeklyAnalysisAsync(CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            _logger.LogWarning("Atlas not configured — weekly analysis unavailable");
            return new WeeklyAnalysisReport
            {
                Success = false,
                Error = "Atlas not configured"
            };
        }

        try
        {
            var weekAgo = DateTime.UtcNow.AddDays(-7);
            var report = new WeeklyAnalysisReport
            {
                Success = true,
                PeriodStart = weekAgo,
                PeriodEnd = DateTime.UtcNow,
                GeneratedAt = DateTime.UtcNow
            };

            // Interactions & feedback
            var interactions = await _atlas.Interactions
                .CountDocumentsAsync(d => d["created_at"] >= weekAgo, cancellationToken: ct);
            var feedback = await _atlas.Feedback
                .CountDocumentsAsync(d => d["created_at"] >= weekAgo, cancellationToken: ct);
            var feedbackDocs = await _atlas.Feedback
                .Find(d => d["created_at"] >= weekAgo)
                .ToListAsync(ct);
            var avgRating = feedbackDocs.Count > 0
                ? feedbackDocs.Average(d => d["stars"].AsInt32)
                : 0;

            report.TotalInteractions = (int)interactions;
            report.FeedbackReceived = (int)feedback;
            report.AverageRating = avgRating;

            // Knowledge gaps
            var gaps = await _gapAnalysis.AnalyzeGapsAsync(since: weekAgo, minOccurrences: 1, ct: ct);
            report.NewKnowledgeGaps = gaps.Count;
            report.CriticalGaps = gaps.Count(g => g.Severity == "critical");

            // Corpus proposals
            var proposals = await _atlas.Corpus
                .CountDocumentsAsync(d =>
                    d["status"] == "proposed" &&
                    d["proposed_at"] >= weekAgo,
                    cancellationToken: ct);
            var approvals = await _atlas.Corpus
                .CountDocumentsAsync(d =>
                    d["status"] == "approved" &&
                    d.Contains("approved_at") &&
                    d["approved_at"] >= weekAgo,
                    cancellationToken: ct);

            report.CorpusProposalsCreated = (int)proposals;
            report.CorpusEntriesApproved = (int)approvals;

            // Events & outcomes
            var events = await _atlas.ConstructionEvents
                .CountDocumentsAsync(d => d["timestamp"] >= weekAgo, cancellationToken: ct);
            var outcomes = await _atlas.ConstructionOutcomes
                .CountDocumentsAsync(d => d["recorded_at"] >= weekAgo, cancellationToken: ct);
            var validatedOutcomes = await _atlas.ConstructionOutcomes
                .CountDocumentsAsync(d =>
                    d["recorded_at"] >= weekAgo &&
                    d["validation_status"] == "validated",
                    cancellationToken: ct);

            report.EventsCaptured = (int)events;
            report.OutcomesRecorded = (int)outcomes;
            report.OutcomesValidated = (int)validatedOutcomes;

            // Learning recommendations
            var recommendations = await _atlas.LearningRecommendations
                .CountDocumentsAsync(d => d["generated_at"] >= weekAgo, cancellationToken: ct);
            var engaged = await _atlas.LearningRecommendations
                .CountDocumentsAsync(d =>
                    d["generated_at"] >= weekAgo &&
                    d["engagement_status"] != "pending",
                    cancellationToken: ct);

            report.RecommendationsGenerated = (int)recommendations;
            report.RecommendationsEngaged = (int)engaged;
            report.EngagementRate = recommendations > 0 ? (double)engaged / recommendations : 0;

            // Store weekly snapshot
            await StoreWeeklySnapshotAsync(report, ct);

            _logger.LogInformation("Weekly analysis complete: {Interactions} interactions, {Gaps} gaps, {Approvals} corpus approvals",
                report.TotalInteractions, report.NewKnowledgeGaps, report.CorpusEntriesApproved);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Weekly analysis failed");
            return new WeeklyAnalysisReport
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Auto-generate corpus proposals from high-confidence knowledge gap corrections.
    /// Proposes entries that have: high frequency, clear pattern, and common user corrections.
    /// </summary>
    public async Task<AutoProposalResult> GenerateAutoProposalsAsync(CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return new AutoProposalResult
            {
                Success = false,
                Error = "Atlas not configured"
            };
        }

        try
        {
            // Find high-confidence gaps (>10 occurrences, avg rating < 1.5)
            var gaps = await _gapAnalysis.AnalyzeGapsAsync(since: DateTime.UtcNow.AddDays(-30), minOccurrences: 10, ct: ct);
            var highConfidenceGaps = gaps.Where(g =>
                g.Occurrences >= 10 &&
                g.AverageRating < 1.5 &&
                g.SampleComments.Count >= 3).ToList();

            var proposalsCreated = 0;

            foreach (var gap in highConfidenceGaps.Take(5)) // Limit to top 5 per run
            {
                // Extract common corrections from comments
                var corrections = ExtractCorrectionsFromComments(gap.SampleComments);
                if (string.IsNullOrWhiteSpace(corrections))
                    continue;

                // Get gap detail for provenance
                var gapDetail = await _gapAnalysis.GetGapDetailAsync(gap.QueryPattern, ct);
                if (gapDetail == null)
                    continue;

                // Create auto-generated proposal
                var proposal = await _corpusImprovement.ProposeEntryAsync(new ProposeEntryRequest
                {
                    Title = $"Improved guidance for: {gap.QueryPattern}",
                    Content = corrections,
                    Tags = [gap.Category, "auto-generated"],
                    Scope = "internal",
                    Category = gap.Category,
                    ProposedBy = "system-auto",
                    Rationale = $"Auto-generated from {gap.Occurrences} low-rated interactions (avg {gap.AverageRating:F2} stars). Common user feedback suggests this correction.",
                    SourceInteractionIds = gapDetail.InteractionIds,
                    SourceQueryPattern = gap.QueryPattern,
                    ValidatedAnswer = corrections
                }, ct);

                if (proposal != null)
                {
                    // Mark as auto-generated
                    await _atlas.Corpus.UpdateOneAsync(
                        Builders<BsonDocument>.Filter.Eq("_id", proposal.Id),
                        Builders<BsonDocument>.Update.Set("auto_generated", true),
                        cancellationToken: ct);

                    proposalsCreated++;
                    _logger.LogInformation("Auto-generated proposal: {ProposalId} for gap pattern: {Pattern}",
                        proposal.Id, gap.QueryPattern);
                }
            }

            return new AutoProposalResult
            {
                Success = true,
                ProposalsCreated = proposalsCreated,
                HighConfidenceGaps = highConfidenceGaps.Count,
                GeneratedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-proposal generation failed");
            return new AutoProposalResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Calculate quality trends over time by comparing recent metrics to historical baseline.
    /// </summary>
    public async Task<QualityTrendsReport> CalculateQualityTrendsAsync(
        int weeks = 4,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return new QualityTrendsReport
            {
                Success = false,
                Error = "Atlas not configured"
            };
        }

        try
        {
            var snapshots = await _atlas.QualityMetrics
                .Find(Builders<BsonDocument>.Filter.Empty)
                .SortByDescending(d => d["period_end"])
                .Limit(weeks)
                .ToListAsync(ct);

            if (snapshots.Count == 0)
            {
                return new QualityTrendsReport
                {
                    Success = true,
                    Weeks = 0,
                    Message = "No historical data available yet"
                };
            }

            var latest = snapshots[0];
            var oldest = snapshots[^1];

            return new QualityTrendsReport
            {
                Success = true,
                Weeks = snapshots.Count,
                LatestAverageRating = latest.GetValue("average_rating", 0.0).ToDouble(),
                LatestAverageConfidence = latest.GetValue("average_confidence", 0.0).ToDouble(),
                LatestGapCount = latest.GetValue("gap_count", 0).AsInt32,
                LatestCorpusSize = latest.GetValue("corpus_size", 0).AsInt32,
                OldestAverageRating = oldest.GetValue("average_rating", 0.0).ToDouble(),
                OldestAverageConfidence = oldest.GetValue("average_confidence", 0.0).ToDouble(),
                OldestGapCount = oldest.GetValue("gap_count", 0).AsInt32,
                OldestCorpusSize = oldest.GetValue("corpus_size", 0).AsInt32,
                Trend = CalculateTrend(latest, oldest)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quality trends calculation failed");
            return new QualityTrendsReport
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Generate comprehensive learning pipeline health report.
    /// </summary>
    public async Task<PipelineHealthReport> GenerateImprovementReportAsync(
        string period = "week",
        CancellationToken ct = default)
    {
        var since = period switch
        {
            "day" => DateTime.UtcNow.AddDays(-1),
            "week" => DateTime.UtcNow.AddDays(-7),
            "month" => DateTime.UtcNow.AddDays(-30),
            _ => DateTime.UtcNow.AddDays(-7)
        };

        var weeklyReport = await RunWeeklyAnalysisAsync(ct);
        var trends = await CalculateQualityTrendsAsync(4, ct);

        return new PipelineHealthReport
        {
            Period = period,
            Since = since,
            WeeklyMetrics = weeklyReport,
            QualityTrends = trends,
            OverallHealth = CalculateOverallHealth(weeklyReport, trends),
            GeneratedAt = DateTime.UtcNow
        };
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task StoreWeeklySnapshotAsync(WeeklyAnalysisReport report, CancellationToken ct)
    {
        try
        {
            var snapshot = new BsonDocument
            {
                ["_id"] = Guid.NewGuid().ToString(),
                ["period_start"] = report.PeriodStart,
                ["period_end"] = report.PeriodEnd,
                ["total_interactions"] = report.TotalInteractions,
                ["feedback_received"] = report.FeedbackReceived,
                ["average_rating"] = report.AverageRating,
                ["gap_count"] = report.NewKnowledgeGaps,
                ["critical_gaps"] = report.CriticalGaps,
                ["corpus_size"] = report.CorpusEntriesApproved,
                ["recommendations_generated"] = report.RecommendationsGenerated,
                ["engagement_rate"] = report.EngagementRate,
                ["average_confidence"] = 0.0, // Would calculate from interactions
                ["created_at"] = DateTime.UtcNow
            };

            await _atlas.QualityMetrics.InsertOneAsync(snapshot, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store weekly snapshot");
        }
    }

    private static string ExtractCorrectionsFromComments(List<string> comments)
    {
        // Simple heuristic: find comments that suggest corrections or improvements
        var corrections = comments
            .Where(c => c.Contains("should", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("need", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("missing", StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToList();

        return corrections.Count > 0
            ? string.Join(" ", corrections)
            : "";
    }

    private static string CalculateTrend(BsonDocument latest, BsonDocument oldest)
    {
        var ratingImprovement = latest.GetValue("average_rating", 0.0).ToDouble() -
                               oldest.GetValue("average_rating", 0.0).ToDouble();
        var gapReduction = oldest.GetValue("gap_count", 0).AsInt32 -
                          latest.GetValue("gap_count", 0).AsInt32;

        if (ratingImprovement > 0.1 && gapReduction > 0)
            return "improving";
        if (ratingImprovement < -0.1 || gapReduction < -5)
            return "declining";

        return "stable";
    }

    private static string CalculateOverallHealth(WeeklyAnalysisReport weekly, QualityTrendsReport trends)
    {
        var healthScore = 0;

        // Positive indicators
        if (weekly.AverageRating >= 4.0) healthScore += 25;
        else if (weekly.AverageRating >= 3.5) healthScore += 15;

        if (weekly.CorpusEntriesApproved > 0) healthScore += 20;
        if (weekly.EngagementRate >= 0.3) healthScore += 20;
        if (trends.Trend == "improving") healthScore += 20;
        else if (trends.Trend == "stable") healthScore += 10;

        if (weekly.CriticalGaps == 0) healthScore += 15;

        return healthScore switch
        {
            >= 85 => "excellent",
            >= 70 => "good",
            >= 50 => "fair",
            _ => "needs attention"
        };
    }
}

// ── Value objects ──────────────────────────────────────────────────────────────

public sealed class WeeklyAnalysisReport
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public int TotalInteractions { get; init; }
    public int FeedbackReceived { get; init; }
    public double AverageRating { get; init; }
    public int NewKnowledgeGaps { get; init; }
    public int CriticalGaps { get; init; }
    public int CorpusProposalsCreated { get; init; }
    public int CorpusEntriesApproved { get; init; }
    public int EventsCaptured { get; init; }
    public int OutcomesRecorded { get; init; }
    public int OutcomesValidated { get; init; }
    public int RecommendationsGenerated { get; init; }
    public int RecommendationsEngaged { get; init; }
    public double EngagementRate { get; init; }
    public DateTime GeneratedAt { get; init; }
}

public sealed class AutoProposalResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public int ProposalsCreated { get; init; }
    public int HighConfidenceGaps { get; init; }
    public DateTime GeneratedAt { get; init; }
}

public sealed class QualityTrendsReport
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? Message { get; init; }
    public int Weeks { get; init; }
    public double LatestAverageRating { get; init; }
    public double LatestAverageConfidence { get; init; }
    public int LatestGapCount { get; init; }
    public int LatestCorpusSize { get; init; }
    public double OldestAverageRating { get; init; }
    public double OldestAverageConfidence { get; init; }
    public int OldestGapCount { get; init; }
    public int OldestCorpusSize { get; init; }
    public string Trend { get; init; } = "unknown";
}

public sealed class PipelineHealthReport
{
    public string Period { get; init; } = "week";
    public DateTime Since { get; init; }
    public required WeeklyAnalysisReport WeeklyMetrics { get; init; }
    public required QualityTrendsReport QualityTrends { get; init; }
    public string OverallHealth { get; init; } = "unknown";
    public DateTime GeneratedAt { get; init; }
}

using Auricrux.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

[ApiController]
[Route("api/knowledge")]
public sealed class KnowledgeController(
    KnowledgeGapAnalysisService gapAnalysis,
    CorpusImprovementService corpusImprovement,
    ImprovementEvaluationService evaluation,
    LearningRecommendationService learningRecommendations,
    ContinuousImprovementService continuousImprovement,
    AuditTrailService auditTrail,
    ProvenanceService provenance,
    ILogger<KnowledgeController> logger) : ControllerBase
{
    /// <summary>
    /// Get knowledge gaps identified from low-rated interactions.
    /// Returns aggregated patterns showing where Auricrux needs improvement.
    /// </summary>
    [HttpGet("gaps")]
    public async Task<ActionResult<KnowledgeGapsResponse>> GetGaps(
        [FromQuery] int? days = 30,
        [FromQuery] int? minOccurrences = 2,
        CancellationToken cancellationToken = default)
    {
        var since = days.HasValue ? DateTime.UtcNow.AddDays(-days.Value) : (DateTime?)null;
        var gaps = await gapAnalysis.AnalyzeGapsAsync(since, minOccurrences ?? 2, cancellationToken);

        return Ok(new KnowledgeGapsResponse
        {
            Success = true,
            Gaps = gaps,
            TotalGaps = gaps.Count,
            AnalysisPeriodDays = days ?? 30,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get detailed information about a specific knowledge gap.
    /// </summary>
    [HttpGet("gaps/{queryPattern}")]
    public async Task<ActionResult<KnowledgeGapDetailResponse>> GetGapDetail(
        string queryPattern,
        CancellationToken cancellationToken)
    {
        var detail = await gapAnalysis.GetGapDetailAsync(queryPattern, cancellationToken);

        if (detail == null)
        {
            return NotFound(new { error = "No gap found for the specified query pattern." });
        }

        return Ok(new KnowledgeGapDetailResponse
        {
            Success = true,
            Detail = detail,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Propose a new corpus entry to fill a knowledge gap.
    /// Entry status is "proposed" until approved by reviewer.
    /// </summary>
    [HttpPost("propose-entry")]
    public async Task<ActionResult<ProposeEntryResponse>> ProposeEntry(
        [FromBody] ProposeEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Title and Content are required." });
        }

        var entry = await corpusImprovement.ProposeEntryAsync(request, cancellationToken);

        if (entry == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Atlas not configured or proposal failed." });
        }

        return Ok(new ProposeEntryResponse
        {
            Success = true,
            Entry = entry,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// List all proposed corpus entries awaiting review.
    /// </summary>
    [HttpGet("proposed-entries")]
    public async Task<ActionResult<ProposedEntriesResponse>> ListProposedEntries(
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        var entries = await corpusImprovement.ListProposedEntriesAsync(category, cancellationToken);

        return Ok(new ProposedEntriesResponse
        {
            Success = true,
            Entries = entries,
            TotalEntries = entries.Count,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Approve a proposed corpus entry, moving it to production.
    /// </summary>
    [HttpPost("approve-entry/{proposalId}")]
    public async Task<ActionResult<ApprovalResponse>> ApproveEntry(
        string proposalId,
        [FromBody] ApprovalRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await corpusImprovement.ApproveEntryAsync(
            proposalId,
            request?.ApprovedBy,
            request?.ReviewNotes,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new ApprovalResponse
        {
            Success = true,
            ApprovedEntryId = result.ApprovedEntryId!,
            ApprovedAt = result.ApprovedAt!.Value,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Reject a proposed corpus entry with reason.
    /// </summary>
    [HttpPost("reject-entry/{proposalId}")]
    public async Task<IActionResult> RejectEntry(
        string proposalId,
        [FromBody] RejectionRequest? request,
        CancellationToken cancellationToken)
    {
        var success = await corpusImprovement.RejectEntryAsync(
            proposalId,
            request?.RejectedBy,
            request?.RejectionReason,
            cancellationToken);

        if (!success)
        {
            return BadRequest(new { error = "Proposal not found or already processed." });
        }

        return Ok(new { success = true, timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Evaluate improvement for a specific query.
    /// Tests whether recent corpus additions improved response quality.
    /// </summary>
    [HttpPost("evaluate-improvement")]
    public async Task<ActionResult<ImprovementResult>> EvaluateImprovement(
        [FromBody] EvaluateImprovementRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query is required." });
        }

        var result = await evaluation.EvaluateQueryImprovementAsync(
            request.Query,
            request.ApprovedEntryId,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Evaluate impact of an approved corpus entry.
    /// Tests whether the entry is being retrieved and improving responses.
    /// </summary>
    [HttpGet("evaluate-entry/{approvedEntryId}")]
    public async Task<ActionResult<ApprovedEntryImpactReport>> EvaluateEntryImpact(
        string approvedEntryId,
        CancellationToken cancellationToken)
    {
        var report = await evaluation.EvaluateApprovedEntryImpactAsync(
            approvedEntryId,
            testQueries: null,
            cancellationToken);

        if (!report.Success)
        {
            return BadRequest(new { error = report.Error });
        }

        return Ok(report);
    }

    /// <summary>
    /// Get improvement report dashboard.
    /// Shows overall improvement metrics across all approved entries.
    /// </summary>
    [HttpGet("improvement-dashboard")]
    public ActionResult<object> GetImprovementDashboard()
    {
        // Placeholder for dashboard aggregation
        // Would aggregate data from interactions, feedback, and approved entries
        return Ok(new
        {
            success = true,
            message = "Dashboard aggregation to be implemented",
            timestamp = DateTime.UtcNow
        });
    }

    // ── Learning Recommendations (Phase 7) ─────────────────────────────────────

    /// <summary>
    /// Get personalized learning recommendations for a user.
    /// Recommends specific topics based on user's knowledge gaps and field activity.
    /// </summary>
    [HttpGet("recommendations")]
    public async Task<ActionResult<RecommendationsResponse>> GetRecommendations(
        [FromQuery] string userId,
        [FromQuery] int? limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required." });
        }

        var recommendations = await learningRecommendations.GetRecommendationsForUserAsync(
            userId,
            limit ?? 5,
            cancellationToken);

        return Ok(new RecommendationsResponse
        {
            Success = true,
            Recommendations = recommendations,
            TotalRecommendations = recommendations.Count,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get learning recommendations for a specific knowledge gap.
    /// </summary>
    [HttpGet("recommendations/gap/{pattern}")]
    public async Task<ActionResult<RecommendationsResponse>> GetRecommendationsForGap(
        string pattern,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        var recommendations = await learningRecommendations.GetRecommendationsForGapAsync(
            pattern,
            category,
            cancellationToken);

        return Ok(new RecommendationsResponse
        {
            Success = true,
            Recommendations = recommendations,
            TotalRecommendations = recommendations.Count,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Mark a recommendation as engaged (viewed, started, completed).
    /// </summary>
    [HttpPost("recommendations/{recommendationId}/engage")]
    public async Task<IActionResult> EngageRecommendation(
        string recommendationId,
        [FromBody] EngageRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { error = "Status is required (viewed, started, completed)." });
        }

        var success = await learningRecommendations.TrackRecommendationEngagementAsync(
            recommendationId,
            request.Status,
            cancellationToken);

        if (!success)
        {
            return BadRequest(new { error = "Recommendation not found or Atlas not configured." });
        }

        return Ok(new { success = true, timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Get recommendation effectiveness report.
    /// Shows engagement rate and follow-through metrics.
    /// </summary>
    [HttpGet("recommendations/effectiveness")]
    public async Task<ActionResult<RecommendationEffectivenessResponse>> GetRecommendationEffectiveness(
        [FromQuery] int? days = 30,
        CancellationToken cancellationToken = default)
    {
        var since = days.HasValue ? DateTime.UtcNow.AddDays(-days.Value) : (DateTime?)null;

        var report = await learningRecommendations.GetEffectivenessReportAsync(since, cancellationToken);

        return Ok(new RecommendationEffectivenessResponse
        {
            Success = true,
            Report = report,
            Timestamp = DateTime.UtcNow
        });
    }

    // ── Continuous Improvement & Pipeline Health (Phase 9) ──────────────────────

    /// <summary>
    /// Manually trigger weekly analysis.
    /// Normally runs automatically every Sunday, but can be triggered manually for testing.
    /// </summary>
    [HttpPost("run-analysis")]
    public async Task<ActionResult<WeeklyAnalysisReport>> RunAnalysis(
        CancellationToken cancellationToken)
    {
        var report = await continuousImprovement.RunWeeklyAnalysisAsync(cancellationToken);

        if (!report.Success)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = report.Error });
        }

        return Ok(report);
    }

    /// <summary>
    /// Get quality trends over time.
    /// Shows week-over-week improvements in ratings, confidence, gap count, etc.
    /// </summary>
    [HttpGet("quality-trends")]
    public async Task<ActionResult<QualityTrendsReport>> GetQualityTrends(
        [FromQuery] int? weeks = 4,
        CancellationToken cancellationToken = default)
    {
        var report = await continuousImprovement.CalculateQualityTrendsAsync(weeks ?? 4, cancellationToken);

        if (!report.Success)
        {
            return BadRequest(new { error = report.Error });
        }

        return Ok(report);
    }

    /// <summary>
    /// Get auto-generated corpus proposals.
    /// Lists proposals created automatically from high-confidence knowledge gaps.
    /// </summary>
    [HttpGet("auto-proposals")]
    public async Task<ActionResult<AutoProposalsResponse>> GetAutoProposals(
        CancellationToken cancellationToken)
    {
        // Trigger auto-proposal generation
        var result = await continuousImprovement.GenerateAutoProposalsAsync(cancellationToken);

        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = result.Error });
        }

        return Ok(new AutoProposalsResponse
        {
            Success = true,
            ProposalsCreated = result.ProposalsCreated,
            HighConfidenceGaps = result.HighConfidenceGaps,
            Timestamp = result.GeneratedAt
        });
    }

    /// <summary>
    /// Get learning pipeline health dashboard.
    /// Complete overview of learning loop metrics and trends.
    /// </summary>
    [HttpGet("pipeline-health")]
    public async Task<ActionResult<PipelineHealthReport>> GetPipelineHealth(
        [FromQuery] string? period = "week",
        CancellationToken cancellationToken = default)
    {
        var report = await continuousImprovement.GenerateImprovementReportAsync(period ?? "week", cancellationToken);

        return Ok(report);
    }

    // ── Audit Trail & Provenance (Phase 10) ────────────────────────────────────

    /// <summary>
    /// Query audit trail with filters.
    /// Shows complete history of all learning pipeline actions.
    /// </summary>
    [HttpGet("audit")]
    public async Task<ActionResult<AuditTrailResponse>> QueryAuditTrail(
        [FromQuery] string? actionType = null,
        [FromQuery] string? actorId = null,
        [FromQuery] string? resourceId = null,
        [FromQuery] int? days = 30,
        [FromQuery] int? limit = 100,
        CancellationToken cancellationToken = default)
    {
        var since = days.HasValue ? DateTime.UtcNow.AddDays(-days.Value) : (DateTime?)null;

        var entries = await auditTrail.QueryAuditTrailAsync(
            actionType,
            actorId,
            resourceId,
            since,
            limit ?? 100,
            cancellationToken);

        return Ok(new AuditTrailResponse
        {
            Success = true,
            Entries = entries,
            TotalEntries = entries.Count,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get complete provenance for a corpus entry.
    /// Traces entry back to original interactions and feedback.
    /// </summary>
    [HttpGet("provenance/corpus/{entryId}")]
    public async Task<ActionResult<CorpusProvenanceResponse>> GetCorpusProvenance(
        string entryId,
        CancellationToken cancellationToken)
    {
        var provenance = await provenance.GetCorpusEntryProvenanceAsync(entryId, cancellationToken);

        if (provenance == null)
        {
            return NotFound(new { error = "Corpus entry not found or Atlas not configured." });
        }

        return Ok(new CorpusProvenanceResponse
        {
            Success = true,
            Provenance = provenance,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get provenance for a knowledge gap.
    /// Shows all interactions and feedback contributing to the gap.
    /// </summary>
    [HttpGet("provenance/gap/{pattern}")]
    public async Task<ActionResult<GapProvenanceResponse>> GetGapProvenance(
        string pattern,
        CancellationToken cancellationToken)
    {
        var gapProvenance = await provenance.GetGapAnalysisProvenanceAsync(pattern, cancellationToken);

        if (gapProvenance == null)
        {
            return NotFound(new { error = "Gap not found or Atlas not configured." });
        }

        return Ok(new GapProvenanceResponse
        {
            Success = true,
            Provenance = gapProvenance,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get provenance for a learning recommendation.
    /// Shows events and outcomes that led to the recommendation.
    /// </summary>
    [HttpGet("provenance/recommendation/{recommendationId}")]
    public async Task<ActionResult<RecommendationProvenanceResponse>> GetRecommendationProvenance(
        string recommendationId,
        CancellationToken cancellationToken)
    {
        var recProvenance = await provenance.GetRecommendationProvenanceAsync(recommendationId, cancellationToken);

        if (recProvenance == null)
        {
            return NotFound(new { error = "Recommendation not found or Atlas not configured." });
        }

        return Ok(new RecommendationProvenanceResponse
        {
            Success = true,
            Provenance = recProvenance,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get complete observability dashboard.
    /// Combines pipeline health, audit trail, and provenance metrics.
    /// </summary>
    [HttpGet("observability-dashboard")]
    public async Task<ActionResult<object>> GetObservabilityDashboard(
        CancellationToken cancellationToken)
    {
        // Get pipeline health
        var health = await continuousImprovement.GenerateImprovementReportAsync("week", cancellationToken);

        // Get recent audit actions
        var recentAudit = await auditTrail.QueryAuditTrailAsync(
            actionType: null,
            actorId: null,
            resourceId: null,
            since: DateTime.UtcNow.AddDays(-7),
            limit: 50,
            ct: cancellationToken);

        return Ok(new
        {
            success = true,
            pipeline_health = health,
            recent_audit_count = recentAudit.Count,
            recent_actions = recentAudit.Take(10).Select(a => new
            {
                action = a.ActionType,
                actor = a.ActorId,
                resource = a.ResourceId,
                timestamp = a.Timestamp
            }),
            timestamp = DateTime.UtcNow
        });
    }
}

// ── Response models ────────────────────────────────────────────────────────────

public sealed class KnowledgeGapsResponse
{
    public bool Success { get; init; }
    public List<KnowledgeGap> Gaps { get; init; } = [];
    public int TotalGaps { get; init; }
    public int AnalysisPeriodDays { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class KnowledgeGapDetailResponse
{
    public bool Success { get; init; }
    public required KnowledgeGapDetail Detail { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class ProposeEntryResponse
{
    public bool Success { get; init; }
    public required ProposedEntry Entry { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class ProposedEntriesResponse
{
    public bool Success { get; init; }
    public List<ProposedEntry> Entries { get; init; } = [];
    public int TotalEntries { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class ApprovalRequest
{
    public string? ApprovedBy { get; init; }
    public string? ReviewNotes { get; init; }
}

public sealed class ApprovalResponse
{
    public bool Success { get; init; }
    public required string ApprovedEntryId { get; init; }
    public DateTime ApprovedAt { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class RejectionRequest
{
    public string? RejectedBy { get; init; }
    public string? RejectionReason { get; init; }
}

public sealed class EvaluateImprovementRequest
{
    public required string Query { get; init; }
    public string? ApprovedEntryId { get; init; }
}

public sealed class RecommendationsResponse
{
    public bool Success { get; init; }
    public List<LearningRecommendation> Recommendations { get; init; } = [];
    public int TotalRecommendations { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class EngageRecommendationRequest
{
    public required string Status { get; init; } // "viewed", "started", "completed"
}

public sealed class RecommendationEffectivenessResponse
{
    public bool Success { get; init; }
    public required RecommendationEffectivenessReport Report { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class AutoProposalsResponse
{
    public bool Success { get; init; }
    public int ProposalsCreated { get; init; }
    public int HighConfidenceGaps { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class AuditTrailResponse
{
    public bool Success { get; init; }
    public List<AuditEntry> Entries { get; init; } = [];
    public int TotalEntries { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class CorpusProvenanceResponse
{
    public bool Success { get; init; }
    public required CorpusProvenance Provenance { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class GapProvenanceResponse
{
    public bool Success { get; init; }
    public required GapProvenance Provenance { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class RecommendationProvenanceResponse
{
    public bool Success { get; init; }
    public required RecommendationProvenance Provenance { get; init; }
    public DateTime Timestamp { get; init; }
}

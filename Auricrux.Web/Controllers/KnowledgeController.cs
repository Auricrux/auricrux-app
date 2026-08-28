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

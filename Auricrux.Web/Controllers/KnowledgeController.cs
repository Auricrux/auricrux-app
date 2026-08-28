using Auricrux.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

[ApiController]
[Route("api/knowledge")]
public sealed class KnowledgeController(
    KnowledgeGapAnalysisService gapAnalysis,
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

using Auricrux.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

/// <summary>
/// Phase 9A: Predictive Intelligence API
/// Exposes the "nearly impossible" breakthrough feature
/// </summary>
[ApiController]
[Route("api/predictive")]
public class PredictiveIntelligenceController : ControllerBase
{
    private readonly PredictiveIntelligenceService _predictive;
    private readonly AcademyLessonMatcherService _lessonMatcher;
    private readonly FcaEcosystemApiService _fca;
    private readonly ILogger<PredictiveIntelligenceController> _logger;

    public PredictiveIntelligenceController(
        PredictiveIntelligenceService predictive,
        AcademyLessonMatcherService lessonMatcher,
        FcaEcosystemApiService fca,
        ILogger<PredictiveIntelligenceController> logger)
    {
        _predictive = predictive;
        _lessonMatcher = lessonMatcher;
        _fca = fca;
        _logger = logger;
    }

    /// <summary>
    /// Trigger predictive intelligence transfer for an outcome
    /// When called, analyzes the outcome and proactively delivers knowledge to similar projects
    /// </summary>
    [HttpPost("transfer/{outcomeId}")]
    public async Task<IActionResult> TriggerIntelligenceTransfer(
        string outcomeId,
        [FromQuery] string? sourceProjectId = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("API trigger: Predictive intelligence transfer for outcome {OutcomeId}", outcomeId);

            var transferredCount = await _predictive.PredictAndTransferKnowledgeAsync(
                outcomeId,
                sourceProjectId ?? "unknown",
                ct);

            return Ok(new
            {
                outcome_id = outcomeId,
                projects_notified = transferredCount,
                status = "completed",
                message = $"Predictive knowledge transferred to {transferredCount} similar projects"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in predictive intelligence transfer API");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Link all unlinked recommendations to Academy lessons
    /// Backfills lesson IDs for existing recommendations
    /// </summary>
    [HttpPost("link-lessons")]
    public async Task<IActionResult> LinkRecommendationsToLessons(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("API trigger: Linking recommendations to Academy lessons");

            var linkedCount = await _lessonMatcher.LinkRecommendationsToLessonsAsync(ct);

            return Ok(new
            {
                linked_count = linkedCount,
                status = "completed",
                message = $"Successfully linked {linkedCount} recommendations to Academy lessons"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking recommendations to lessons");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get predictive recommendations for a specific project
    /// Shows what knowledge has been proactively delivered
    /// </summary>
    [HttpGet("recommendations/{projectId}")]
    public async Task<IActionResult> GetPredictiveRecommendations(
        string projectId,
        CancellationToken ct = default)
    {
        try
        {
            // Validate project exists
            if (Guid.TryParse(projectId, out var guid))
            {
                var project = await _fca.GetProjectAsync(guid, ct);
                if (project == null)
                {
                    return NotFound(new { error = "Project not found" });
                }
            }

            // Query would go here - for now return placeholder
            return Ok(new
            {
                project_id = projectId,
                predictive_recommendations = new object[] { },
                message = "Predictive recommendations query - implementation in progress"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching predictive recommendations");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Health check for predictive intelligence system
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            service = "predictive-intelligence",
            status = "operational",
            capabilities = new[]
            {
                "cross_project_similarity",
                "causal_factor_extraction",
                "proactive_knowledge_delivery",
                "academy_lesson_matching"
            },
            timestamp = DateTime.UtcNow
        });
    }
}

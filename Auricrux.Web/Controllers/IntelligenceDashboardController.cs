using Auricrux.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

/// <summary>
/// Phase 9B: Intelligence Dashboard API
/// Exposes aggregated metrics and real-time observability data
/// </summary>
[ApiController]
[Route("api/intelligence/dashboard")]
public class IntelligenceDashboardController : ControllerBase
{
    private readonly IntelligenceDashboardService _dashboard;
    private readonly ILogger<IntelligenceDashboardController> _logger;

    public IntelligenceDashboardController(
        IntelligenceDashboardService dashboard,
        ILogger<IntelligenceDashboardController> logger)
    {
        _dashboard = dashboard;
        _logger = logger;
    }

    /// <summary>
    /// Get dashboard overview metrics
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        [FromQuery] string period = "24h",
        CancellationToken ct = default)
    {
        try
        {
            var timeSpan = period switch
            {
                "24h" => TimeSpan.FromHours(24),
                "7d" => TimeSpan.FromDays(7),
                "30d" => TimeSpan.FromDays(30),
                _ => TimeSpan.FromHours(24)
            };

            var overview = await _dashboard.GetOverviewAsync(timeSpan, ct);
            return Ok(overview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard overview");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get learning loop stage metrics
    /// </summary>
    [HttpGet("learning-loop")]
    public async Task<IActionResult> GetLearningLoop(CancellationToken ct = default)
    {
        try
        {
            var metrics = await _dashboard.GetLearningLoopMetricsAsync(ct);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting learning loop metrics");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get recent predictive intelligence transfers
    /// </summary>
    [HttpGet("predictive-transfers")]
    public async Task<IActionResult> GetPredictiveTransfers(
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        try
        {
            var transfers = await _dashboard.GetRecentTransfersAsync(limit, ct);
            return Ok(new
            {
                transfers,
                total_active = transfers.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting predictive transfers");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get active knowledge gaps
    /// </summary>
    [HttpGet("knowledge-gaps")]
    public async Task<IActionResult> GetKnowledgeGaps(
        [FromQuery] string status = "active",
        CancellationToken ct = default)
    {
        try
        {
            var gaps = await _dashboard.GetActiveKnowledgeGapsAsync(ct);
            
            var totalActive = gaps.Count;
            var withRecommendations = gaps.Count(g => g.RecommendationsCount > 0);
            var coveragePercentage = totalActive > 0 
                ? (withRecommendations / (double)totalActive) * 100 
                : 0;

            return Ok(new
            {
                gaps,
                total_active = totalActive,
                coverage_percentage = Math.Round(coveragePercentage, 1)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting knowledge gaps");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get recent audit trail actions
    /// </summary>
    [HttpGet("audit-trail")]
    public async Task<IActionResult> GetAuditTrail(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        try
        {
            var actions = await _dashboard.GetRecentAuditActionsAsync(limit, ct);
            return Ok(new { actions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit trail");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Health check for dashboard services
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            service = "intelligence-dashboard",
            status = "operational",
            endpoints = new[]
            {
                "/api/intelligence/dashboard/overview",
                "/api/intelligence/dashboard/learning-loop",
                "/api/intelligence/dashboard/predictive-transfers",
                "/api/intelligence/dashboard/knowledge-gaps",
                "/api/intelligence/dashboard/audit-trail"
            },
            timestamp = DateTime.UtcNow
        });
    }
}

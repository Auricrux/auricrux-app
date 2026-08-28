using Auricrux.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

[ApiController]
[Route("api/context")]
public sealed class ContextController(
    ContextAwareGuidanceService contextService,
    ILogger<ContextController> logger) : ControllerBase
{
    /// <summary>
    /// Get recent construction activity for user/project to understand context.
    /// </summary>
    [HttpGet("recent-activity")]
    public async Task<ActionResult<RecentActivityResponse>> GetRecentActivity(
        [FromQuery] string? userId = null,
        [FromQuery] string? projectId = null,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(projectId))
        {
            return BadRequest(new { error = "Either userId or projectId must be provided." });
        }

        var events = await contextService.GetRecentContextAsync(userId, projectId, limit, cancellationToken);

        return Ok(new RecentActivityResponse
        {
            Success = true,
            Events = events.Select(e => new ActivitySummary
            {
                EventId = e.EventId,
                EventType = e.EventType,
                ActivityDescription = e.ActivityDescription,
                Timestamp = e.Timestamp,
                UserId = e.UserId,
                ProjectId = e.ProjectId,
                Phase = e.Phase,
                Task = e.Task
            }).ToList(),
            TotalEvents = events.Count,
            UserId = userId,
            ProjectId = projectId,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Track guidance effectiveness by linking an interaction to a field event outcome.
    /// This enables measuring whether Auricrux guidance led to positive results.
    /// </summary>
    [HttpPost("track-effectiveness")]
    public async Task<IActionResult> TrackEffectiveness(
        [FromBody] TrackEffectivenessRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InteractionId) || string.IsNullOrWhiteSpace(request.EventId))
        {
            return BadRequest(new { error = "Both InteractionId and EventId are required." });
        }

        var success = await contextService.TrackGuidanceEffectivenessAsync(
            request.InteractionId,
            request.EventId,
            cancellationToken);

        if (!success)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Atlas not configured or tracking failed." });
        }

        return Ok(new { success = true, timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Get guidance effectiveness report for a user.
    /// Shows how often guidance led to positive field outcomes.
    /// </summary>
    [HttpGet("guidance-effectiveness")]
    public async Task<ActionResult<GuidanceEffectivenessResponse>> GetGuidanceEffectiveness(
        [FromQuery] string? userId = null,
        [FromQuery] int? days = 30,
        CancellationToken cancellationToken = default)
    {
        var since = days.HasValue ? DateTime.UtcNow.AddDays(-days.Value) : (DateTime?)null;

        var report = await contextService.GetGuidanceEffectivenessAsync(userId, since, cancellationToken);

        return Ok(new GuidanceEffectivenessResponse
        {
            Success = true,
            Report = report,
            Timestamp = DateTime.UtcNow
        });
    }
}

// ── Response models ────────────────────────────────────────────────────────────

public sealed class RecentActivityResponse
{
    public bool Success { get; init; }
    public List<ActivitySummary> Events { get; init; } = [];
    public int TotalEvents { get; init; }
    public string? UserId { get; init; }
    public string? ProjectId { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class ActivitySummary
{
    public required string EventId { get; init; }
    public required string EventType { get; init; }
    public string ActivityDescription { get; init; } = "";
    public DateTime Timestamp { get; init; }
    public string? UserId { get; init; }
    public string? ProjectId { get; init; }
    public string? Phase { get; init; }
    public string? Task { get; init; }
}

public sealed class TrackEffectivenessRequest
{
    public required string InteractionId { get; init; }
    public required string EventId { get; init; }
}

public sealed class GuidanceEffectivenessResponse
{
    public bool Success { get; init; }
    public required GuidanceEffectivenessReport Report { get; init; }
    public DateTime Timestamp { get; init; }
}

using Auricrux.Shared.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services;

/// <summary>
/// Enhances Auricrux guidance with user/project/role/phase context from recent construction events.
/// Provides personalized, situation-specific responses instead of generic guidance.
/// Tracks guidance effectiveness by linking interactions to subsequent event outcomes.
/// </summary>
public sealed class ContextAwareGuidanceService
{
    private readonly AtlasService _atlas;
    private readonly ConstructionEventService _eventService;
    private readonly ILogger<ContextAwareGuidanceService> _logger;

    public ContextAwareGuidanceService(
        AtlasService atlas,
        ConstructionEventService eventService,
        ILogger<ContextAwareGuidanceService> logger)
    {
        _atlas = atlas;
        _eventService = eventService;
        _logger = logger;
    }

    /// <summary>
    /// Get recent construction events for user/project to understand current context.
    /// </summary>
    public async Task<List<ConstructionEvent>> GetRecentContextAsync(
        string? userId = null,
        string? projectId = null,
        int limit = 10,
        CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-7); // Last 7 days
        return await _eventService.QueryEventsAsync(
            projectId: projectId,
            userId: userId,
            interactionId: null,
            since: since,
            limit: limit,
            ct: ct);
    }

    /// <summary>
    /// Build context-enhanced prompt by analyzing recent user/project activity.
    /// Returns enhanced query with context summary for LLM.
    /// </summary>
    public async Task<ContextEnhancedQuery> BuildContextPromptAsync(
        string query,
        string? userId = null,
        string? projectId = null,
        string? role = null,
        string? phase = null,
        CancellationToken ct = default)
    {
        try
        {
            var recentEvents = await GetRecentContextAsync(userId, projectId, limit: 10, ct);

            if (recentEvents.Count == 0)
            {
                // No recent context available
                return new ContextEnhancedQuery
                {
                    OriginalQuery = query,
                    EnhancedQuery = query,
                    ContextSummary = null,
                    RecentEventCount = 0
                };
            }

            // Analyze recent events to build context summary
            var contextSummary = AnalyzeRecentEvents(recentEvents, role, phase);

            // Build enhanced query with context preamble
            var enhancedQuery = BuildEnhancedQuery(query, contextSummary, role, phase);

            _logger.LogInformation("Context-enhanced query for user={UserId} project={ProjectId}: {EventCount} recent events",
                userId, projectId, recentEvents.Count);

            return new ContextEnhancedQuery
            {
                OriginalQuery = query,
                EnhancedQuery = enhancedQuery,
                ContextSummary = contextSummary,
                RecentEventCount = recentEvents.Count,
                UserId = userId,
                ProjectId = projectId,
                Role = role,
                Phase = phase
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build context prompt - falling back to original query");
            return new ContextEnhancedQuery
            {
                OriginalQuery = query,
                EnhancedQuery = query,
                ContextSummary = null,
                RecentEventCount = 0
            };
        }
    }

    /// <summary>
    /// Find construction events relevant to current query context.
    /// Useful for understanding what user was recently working on related to this topic.
    /// </summary>
    public async Task<List<ConstructionEvent>> GetRelevantEventsForQueryAsync(
        string query,
        string? userId = null,
        string? projectId = null,
        CancellationToken ct = default)
    {
        var recentEvents = await GetRecentContextAsync(userId, projectId, limit: 20, ct);

        if (recentEvents.Count == 0)
            return [];

        // Simple relevance: keyword matching in activity descriptions
        var queryKeywords = ExtractKeywords(query);
        var relevantEvents = recentEvents
            .Where(e => ContainsAnyKeyword(e.ActivityDescription, queryKeywords))
            .Take(5)
            .ToList();

        return relevantEvents;
    }

    /// <summary>
    /// Track guidance effectiveness by linking an interaction to a subsequent event outcome.
    /// Enables measuring whether Auricrux guidance led to positive field results.
    /// </summary>
    public async Task<bool> TrackGuidanceEffectivenessAsync(
        string interactionId,
        string eventId,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            _logger.LogWarning("Atlas not configured - guidance effectiveness tracking unavailable");
            return false;
        }

        try
        {
            // Store effectiveness linkage in guidance_effectiveness collection
            var doc = new BsonDocument
            {
                ["_id"] = Guid.NewGuid().ToString(),
                ["interaction_id"] = interactionId,
                ["event_id"] = eventId,
                ["linked_at"] = DateTime.UtcNow
            };

            await _atlas.GuidanceEffectiveness.InsertOneAsync(doc, cancellationToken: ct);

            _logger.LogInformation("Tracked guidance effectiveness: interaction={InteractionId} → event={EventId}",
                interactionId, eventId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track guidance effectiveness");
            return false;
        }
    }

    /// <summary>
    /// Get guidance effectiveness report for a user.
    /// Shows how often user's subsequent events had positive outcomes after Auricrux guidance.
    /// </summary>
    public async Task<GuidanceEffectivenessReport> GetGuidanceEffectivenessAsync(
        string? userId = null,
        DateTime? since = null,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return new GuidanceEffectivenessReport
            {
                UserId = userId,
                TotalGuidanceInteractions = 0,
                TrackedEvents = 0,
                PositiveOutcomes = 0,
                EffectivenessRate = 0
            };
        }

        try
        {
            var sinceDate = since ?? DateTime.UtcNow.AddDays(-30);

            // Get all guidance effectiveness links for this user
            var effectiveness = await _atlas.GuidanceEffectiveness
                .Find(d => d["linked_at"] >= sinceDate)
                .ToListAsync(ct);

            if (effectiveness.Count == 0)
            {
                return new GuidanceEffectivenessReport
                {
                    UserId = userId,
                    TotalGuidanceInteractions = 0,
                    TrackedEvents = 0,
                    PositiveOutcomes = 0,
                    EffectivenessRate = 0
                };
            }

            // Get event IDs and check their outcomes
            var eventIds = effectiveness.Select(e => e["event_id"].AsString).ToList();
            var positiveCount = 0;

            foreach (var eventId in eventIds)
            {
                var outcomes = await _eventService.GetOutcomesForEventAsync(eventId, ct);
                if (outcomes.Any(o => o.Status == "success" || o.Status == "completed"))
                {
                    positiveCount++;
                }
            }

            var effectivenessRate = effectiveness.Count > 0
                ? (double)positiveCount / effectiveness.Count
                : 0;

            return new GuidanceEffectivenessReport
            {
                UserId = userId,
                TotalGuidanceInteractions = effectiveness.Count,
                TrackedEvents = effectiveness.Count,
                PositiveOutcomes = positiveCount,
                EffectivenessRate = effectivenessRate,
                Since = sinceDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get guidance effectiveness");
            return new GuidanceEffectivenessReport
            {
                UserId = userId,
                TotalGuidanceInteractions = 0,
                TrackedEvents = 0,
                PositiveOutcomes = 0,
                EffectivenessRate = 0
            };
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static string AnalyzeRecentEvents(
        List<ConstructionEvent> events,
        string? role,
        string? phase)
    {
        var eventTypes = events.GroupBy(e => e.EventType)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => $"{g.Key} ({g.Count()})")
            .ToList();

        var activities = events
            .Where(e => !string.IsNullOrWhiteSpace(e.ActivityDescription))
            .Select(e => e.ActivityDescription)
            .Distinct()
            .Take(3)
            .ToList();

        var summary = "Recent activity: ";
        if (eventTypes.Any())
        {
            summary += $"{string.Join(", ", eventTypes)}. ";
        }

        if (activities.Any())
        {
            summary += $"Recent work: {string.Join("; ", activities)}.";
        }

        return summary.Trim();
    }

    private static string BuildEnhancedQuery(
        string query,
        string contextSummary,
        string? role,
        string? phase)
    {
        var prefix = "[CONTEXT] ";

        if (!string.IsNullOrWhiteSpace(role))
        {
            prefix += $"User role: {role}. ";
        }

        if (!string.IsNullOrWhiteSpace(phase))
        {
            prefix += $"Project phase: {phase}. ";
        }

        prefix += contextSummary + "\n\n[QUERY] ";

        return prefix + query;
    }

    private static List<string> ExtractKeywords(string text)
    {
        // Simple keyword extraction: split on spaces and common separators
        var words = text.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '?', '!', ';', ':', '-', '_' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3) // Skip short words
            .Distinct()
            .ToList();

        return words;
    }

    private static bool ContainsAnyKeyword(string text, List<string> keywords)
    {
        var lowerText = text.ToLowerInvariant();
        return keywords.Any(k => lowerText.Contains(k));
    }
}

// ── Value objects ──────────────────────────────────────────────────────────────

public sealed class ContextEnhancedQuery
{
    public required string OriginalQuery { get; init; }
    public required string EnhancedQuery { get; init; }
    public string? ContextSummary { get; init; }
    public int RecentEventCount { get; init; }
    public string? UserId { get; init; }
    public string? ProjectId { get; init; }
    public string? Role { get; init; }
    public string? Phase { get; init; }
}

public sealed class GuidanceEffectivenessReport
{
    public string? UserId { get; init; }
    public int TotalGuidanceInteractions { get; init; }
    public int TrackedEvents { get; init; }
    public int PositiveOutcomes { get; init; }
    public double EffectivenessRate { get; init; }
    public DateTime? Since { get; init; }
}

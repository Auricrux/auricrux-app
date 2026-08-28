using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services;

/// <summary>
/// Complete audit trail for all learning pipeline actions.
/// Records every action with actor, resource, details, and result for compliance and observability.
/// Enables "who did what when" queries across the entire learning loop.
/// </summary>
public sealed class AuditTrailService
{
    private readonly AtlasService _atlas;
    private readonly ILogger<AuditTrailService> _logger;

    public AuditTrailService(AtlasService atlas, ILogger<AuditTrailService> logger)
    {
        _atlas = atlas;
        _logger = logger;
    }

    /// <summary>
    /// Record an action in the audit trail.
    /// </summary>
    public async Task<string?> RecordActionAsync(
        string actionType,
        string actorType,
        string actorId,
        string resourceType,
        string resourceId,
        BsonDocument? actionDetails = null,
        string? result = null,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            _logger.LogWarning("Atlas not configured — audit trail recording unavailable");
            return null;
        }

        try
        {
            var auditId = $"audit_{Guid.NewGuid()}";
            var doc = new BsonDocument
            {
                ["_id"] = auditId,
                ["audit_id"] = auditId,
                ["timestamp"] = DateTime.UtcNow,
                ["action_type"] = actionType,
                ["actor_type"] = actorType,
                ["actor_id"] = actorId,
                ["resource_type"] = resourceType,
                ["resource_id"] = resourceId,
                ["action_details"] = actionDetails ?? new BsonDocument(),
                ["result"] = result ?? "success",
                ["correlation_id"] = correlationId ?? Guid.NewGuid().ToString()
            };

            await _atlas.AuditTrail.InsertOneAsync(doc, cancellationToken: ct);

            _logger.LogDebug("Audit recorded: {ActionType} by {ActorType}:{ActorId} on {ResourceType}:{ResourceId}",
                actionType, actorType, actorId, resourceType, resourceId);

            return auditId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record audit action");
            return null;
        }
    }

    /// <summary>
    /// Query audit trail with filters.
    /// </summary>
    public async Task<List<AuditEntry>> QueryAuditTrailAsync(
        string? actionType = null,
        string? actorId = null,
        string? resourceId = null,
        DateTime? since = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return [];
        }

        try
        {
            var filterBuilder = Builders<BsonDocument>.Filter;
            var filters = new List<FilterDefinition<BsonDocument>>();

            if (!string.IsNullOrWhiteSpace(actionType))
                filters.Add(filterBuilder.Eq("action_type", actionType));

            if (!string.IsNullOrWhiteSpace(actorId))
                filters.Add(filterBuilder.Eq("actor_id", actorId));

            if (!string.IsNullOrWhiteSpace(resourceId))
                filters.Add(filterBuilder.Eq("resource_id", resourceId));

            if (since.HasValue)
                filters.Add(filterBuilder.Gte("timestamp", since.Value));

            var filter = filters.Count > 0
                ? filterBuilder.And(filters)
                : filterBuilder.Empty;

            var docs = await _atlas.AuditTrail
                .Find(filter)
                .SortByDescending(d => d["timestamp"])
                .Limit(limit)
                .ToListAsync(ct);

            return docs.Select(MapAuditEntry).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query audit trail");
            return [];
        }
    }

    /// <summary>
    /// Get complete history of a specific resource.
    /// Shows all actions performed on this resource over time.
    /// </summary>
    public async Task<List<AuditEntry>> GetResourceHistoryAsync(
        string resourceType,
        string resourceId,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return [];
        }

        try
        {
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("resource_type", resourceType),
                Builders<BsonDocument>.Filter.Eq("resource_id", resourceId)
            );

            var docs = await _atlas.AuditTrail
                .Find(filter)
                .SortBy(d => d["timestamp"]) // Chronological order
                .ToListAsync(ct);

            return docs.Select(MapAuditEntry).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resource history");
            return [];
        }
    }

    /// <summary>
    /// Verify provenance chain integrity for a resource.
    /// Ensures all expected audit entries exist in correct sequence.
    /// </summary>
    public async Task<ProvenanceVerification> VerifyProvenanceChainAsync(
        string resourceId,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return new ProvenanceVerification
            {
                ResourceId = resourceId,
                IsValid = false,
                Error = "Atlas not configured"
            };
        }

        try
        {
            // Get all audit entries for this resource
            var entries = await GetResourceHistoryAsync("corpus", resourceId, ct);

            if (entries.Count == 0)
            {
                return new ProvenanceVerification
                {
                    ResourceId = resourceId,
                    IsValid = false,
                    Error = "No audit trail found"
                };
            }

            // Verify expected sequence: proposed → reviewed → approved
            var hasProposal = entries.Any(e => e.ActionType == "proposal.created");
            var hasApproval = entries.Any(e => e.ActionType == "proposal.approved");

            var isValid = hasProposal && hasApproval;
            var gaps = new List<string>();

            if (!hasProposal)
                gaps.Add("Missing proposal.created");
            if (!hasApproval)
                gaps.Add("Missing proposal.approved");

            return new ProvenanceVerification
            {
                ResourceId = resourceId,
                IsValid = isValid,
                TotalEntries = entries.Count,
                MissingSteps = gaps,
                FirstAction = entries.First().ActionType,
                LastAction = entries.Last().ActionType,
                CreatedAt = entries.First().Timestamp,
                LastModifiedAt = entries.Last().Timestamp
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify provenance chain");
            return new ProvenanceVerification
            {
                ResourceId = resourceId,
                IsValid = false,
                Error = ex.Message
            };
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static AuditEntry MapAuditEntry(BsonDocument doc)
    {
        return new AuditEntry
        {
            AuditId = doc["audit_id"].AsString,
            Timestamp = doc["timestamp"].ToUniversalTime(),
            ActionType = doc["action_type"].AsString,
            ActorType = doc["actor_type"].AsString,
            ActorId = doc["actor_id"].AsString,
            ResourceType = doc["resource_type"].AsString,
            ResourceId = doc["resource_id"].AsString,
            Result = doc.GetValue("result", "success").AsString,
            CorrelationId = doc.GetValue("correlation_id", "").AsString
        };
    }
}

// ── Value objects ──────────────────────────────────────────────────────────────

public sealed class AuditEntry
{
    public required string AuditId { get; init; }
    public DateTime Timestamp { get; init; }
    public required string ActionType { get; init; }
    public required string ActorType { get; init; }
    public required string ActorId { get; init; }
    public required string ResourceType { get; init; }
    public required string ResourceId { get; init; }
    public string Result { get; init; } = "success";
    public string CorrelationId { get; init; } = "";
}

public sealed class ProvenanceVerification
{
    public required string ResourceId { get; init; }
    public bool IsValid { get; init; }
    public string? Error { get; init; }
    public int TotalEntries { get; init; }
    public List<string> MissingSteps { get; init; } = [];
    public string? FirstAction { get; init; }
    public string? LastAction { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? LastModifiedAt { get; init; }
}

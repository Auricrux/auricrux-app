using Auricrux.Shared.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services;

/// <summary>
/// Captures and stores construction events, outcomes, and evidence for the learning pipeline.
/// Events represent field activities, decisions, and interactions that Auricrux learns from.
/// Outcomes link results back to events. Evidence provides verification.
/// </summary>
public sealed class ConstructionEventService
{
    private readonly AtlasService _atlas;
    private readonly ILogger<ConstructionEventService> _logger;

    public ConstructionEventService(AtlasService atlas, ILogger<ConstructionEventService> logger)
    {
        _atlas = atlas;
        _logger = logger;
    }

    /// <summary>
    /// Record a construction event.
    /// </summary>
    public async Task<ConstructionEvent?> RecordEventAsync(
        RecordEventRequest request,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            _logger.LogWarning("Atlas not configured — event recording unavailable");
            return null;
        }

        try
        {
            var eventId = $"evt_{Guid.NewGuid()}";
            var doc = new BsonDocument
            {
                ["_id"] = eventId,
                ["event_id"] = eventId,
                ["event_type"] = request.EventType,
                ["source"] = request.Source,
                ["timestamp"] = DateTime.UtcNow,
                ["activity_description"] = request.ActivityDescription,
                ["context_data"] = request.ContextData != null 
                    ? BsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(request.ContextData))
                    : new BsonDocument(),
                // Context linkage (optional)
                ["user_id"] = request.UserId ?? "",
                ["role"] = request.Role ?? "",
                ["project_id"] = request.ProjectId ?? "",
                ["job_id"] = request.JobId ?? "",
                ["phase"] = request.Phase ?? "",
                ["task"] = request.Task ?? "",
                // Auricrux integration
                ["interaction_id"] = request.InteractionId ?? "",
                ["triggered_by_auricrux"] = request.TriggeredByAuricrux,
            };

            await _atlas.ConstructionEvents.InsertOneAsync(doc, cancellationToken: ct);

            _logger.LogInformation("Construction event recorded: {EventId} type={EventType}", eventId, request.EventType);

            return new ConstructionEvent
            {
                EventId = eventId,
                EventType = request.EventType,
                Source = request.Source,
                Timestamp = doc["timestamp"].ToUniversalTime(),
                ActivityDescription = request.ActivityDescription,
                ContextData = request.ContextData,
                UserId = request.UserId,
                Role = request.Role,
                ProjectId = request.ProjectId,
                JobId = request.JobId,
                Phase = request.Phase,
                Task = request.Task,
                InteractionId = request.InteractionId,
                TriggeredByAuricrux = request.TriggeredByAuricrux
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record construction event");
            return null;
        }
    }

    /// <summary>
    /// Record an outcome for an event.
    /// </summary>
    public async Task<ConstructionOutcome?> RecordOutcomeAsync(
        RecordOutcomeRequest request,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return null;

        try
        {
            var outcomeId = $"out_{Guid.NewGuid()}";
            var doc = new BsonDocument
            {
                ["_id"] = outcomeId,
                ["outcome_id"] = outcomeId,
                ["event_id"] = request.EventId,
                ["outcome_type"] = request.OutcomeType,
                ["status"] = request.Status,
                ["description"] = request.Description,
                ["measured_result"] = request.MeasuredResult ?? "",
                ["expected_result"] = request.ExpectedResult ?? "",
                ["variance"] = request.Variance ?? "",
                ["validation_status"] = "pending",
                ["recorded_at"] = DateTime.UtcNow,
            };

            await _atlas.ConstructionOutcomes.InsertOneAsync(doc, cancellationToken: ct);

            _logger.LogInformation("Construction outcome recorded: {OutcomeId} for event={EventId}", outcomeId, request.EventId);

            return new ConstructionOutcome
            {
                OutcomeId = outcomeId,
                EventId = request.EventId,
                OutcomeType = request.OutcomeType,
                Status = request.Status,
                Description = request.Description,
                MeasuredResult = request.MeasuredResult,
                ExpectedResult = request.ExpectedResult,
                Variance = request.Variance,
                ValidationStatus = "pending",
                RecordedAt = doc["recorded_at"].ToUniversalTime()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record construction outcome");
            return null;
        }
    }

    /// <summary>
    /// Attach evidence to an outcome.
    /// </summary>
    public async Task<ConstructionEvidence?> AttachEvidenceAsync(
        AttachEvidenceRequest request,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return null;

        try
        {
            var evidenceId = $"evd_{Guid.NewGuid()}";
            var doc = new BsonDocument
            {
                ["_id"] = evidenceId,
                ["evidence_id"] = evidenceId,
                ["outcome_id"] = request.OutcomeId,
                ["evidence_type"] = request.EvidenceType,
                ["file_path"] = request.FilePath ?? "",
                ["url"] = request.Url ?? "",
                ["description"] = request.Description,
                ["metadata"] = request.Metadata != null
                    ? BsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(request.Metadata))
                    : new BsonDocument(),
                ["captured_at"] = DateTime.UtcNow,
                ["captured_by"] = request.CapturedBy ?? "",
                ["verification_status"] = "unverified",
            };

            await _atlas.ConstructionEvidence.InsertOneAsync(doc, cancellationToken: ct);

            _logger.LogInformation("Construction evidence attached: {EvidenceId} to outcome={OutcomeId}", evidenceId, request.OutcomeId);

            return new ConstructionEvidence
            {
                EvidenceId = evidenceId,
                OutcomeId = request.OutcomeId,
                EvidenceType = request.EvidenceType,
                FilePath = request.FilePath,
                Url = request.Url,
                Description = request.Description,
                Metadata = request.Metadata,
                CapturedAt = doc["captured_at"].ToUniversalTime(),
                CapturedBy = request.CapturedBy,
                VerificationStatus = "unverified"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach construction evidence");
            return null;
        }
    }

    /// <summary>
    /// Get events for a project, user, or interaction.
    /// </summary>
    public async Task<List<ConstructionEvent>> QueryEventsAsync(
        string? projectId = null,
        string? userId = null,
        string? interactionId = null,
        DateTime? since = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return [];

        try
        {
            var filterBuilder = Builders<BsonDocument>.Filter;
            var filters = new List<FilterDefinition<BsonDocument>>();

            if (!string.IsNullOrWhiteSpace(projectId))
                filters.Add(filterBuilder.Eq("project_id", projectId));

            if (!string.IsNullOrWhiteSpace(userId))
                filters.Add(filterBuilder.Eq("user_id", userId));

            if (!string.IsNullOrWhiteSpace(interactionId))
                filters.Add(filterBuilder.Eq("interaction_id", interactionId));

            if (since.HasValue)
                filters.Add(filterBuilder.Gte("timestamp", since.Value));

            var filter = filters.Count > 0 
                ? filterBuilder.And(filters) 
                : filterBuilder.Empty;

            var docs = await _atlas.ConstructionEvents
                .Find(filter)
                .SortByDescending(d => d["timestamp"])
                .Limit(limit)
                .ToListAsync(ct);

            return docs.Select(MapEvent).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query construction events");
            return [];
        }
    }

    /// <summary>
    /// Get outcomes for an event.
    /// </summary>
    public async Task<List<ConstructionOutcome>> GetOutcomesForEventAsync(
        string eventId,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return [];

        try
        {
            var docs = await _atlas.ConstructionOutcomes
                .Find(d => d["event_id"] == eventId)
                .ToListAsync(ct);

            return docs.Select(MapOutcome).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get outcomes for event: {EventId}", eventId);
            return [];
        }
    }

    /// <summary>
    /// Get evidence for an outcome.
    /// </summary>
    public async Task<List<ConstructionEvidence>> GetEvidenceForOutcomeAsync(
        string outcomeId,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return [];

        try
        {
            var docs = await _atlas.ConstructionEvidence
                .Find(d => d["outcome_id"] == outcomeId)
                .ToListAsync(ct);

            return docs.Select(MapEvidence).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get evidence for outcome: {OutcomeId}", outcomeId);
            return [];
        }
    }

    private static ConstructionEvent MapEvent(BsonDocument doc)
    {
        return new ConstructionEvent
        {
            EventId = doc["event_id"].AsString,
            EventType = doc["event_type"].AsString,
            Source = doc["source"].AsString,
            Timestamp = doc["timestamp"].ToUniversalTime(),
            ActivityDescription = doc.GetValue("activity_description", "").AsString,
            UserId = doc.Contains("user_id") && !string.IsNullOrWhiteSpace(doc["user_id"].AsString) ? doc["user_id"].AsString : null,
            Role = doc.Contains("role") && !string.IsNullOrWhiteSpace(doc["role"].AsString) ? doc["role"].AsString : null,
            ProjectId = doc.Contains("project_id") && !string.IsNullOrWhiteSpace(doc["project_id"].AsString) ? doc["project_id"].AsString : null,
            JobId = doc.Contains("job_id") && !string.IsNullOrWhiteSpace(doc["job_id"].AsString) ? doc["job_id"].AsString : null,
            Phase = doc.Contains("phase") && !string.IsNullOrWhiteSpace(doc["phase"].AsString) ? doc["phase"].AsString : null,
            Task = doc.Contains("task") && !string.IsNullOrWhiteSpace(doc["task"].AsString) ? doc["task"].AsString : null,
            InteractionId = doc.Contains("interaction_id") && !string.IsNullOrWhiteSpace(doc["interaction_id"].AsString) ? doc["interaction_id"].AsString : null,
            TriggeredByAuricrux = doc.GetValue("triggered_by_auricrux", false).AsBoolean
        };
    }

    private static ConstructionOutcome MapOutcome(BsonDocument doc)
    {
        return new ConstructionOutcome
        {
            OutcomeId = doc["outcome_id"].AsString,
            EventId = doc["event_id"].AsString,
            OutcomeType = doc["outcome_type"].AsString,
            Status = doc["status"].AsString,
            Description = doc.GetValue("description", "").AsString,
            MeasuredResult = doc.Contains("measured_result") ? doc["measured_result"].AsString : null,
            ExpectedResult = doc.Contains("expected_result") ? doc["expected_result"].AsString : null,
            Variance = doc.Contains("variance") ? doc["variance"].AsString : null,
            ValidationStatus = doc.GetValue("validation_status", "pending").AsString,
            ValidatedBy = doc.Contains("validated_by") ? doc["validated_by"].AsString : null,
            ValidatedAt = doc.Contains("validated_at") ? doc["validated_at"].ToUniversalTime() : null,
            ValidationNotes = doc.Contains("validation_notes") ? doc["validation_notes"].AsString : null,
            RecordedAt = doc["recorded_at"].ToUniversalTime()
        };
    }

    private static ConstructionEvidence MapEvidence(BsonDocument doc)
    {
        return new ConstructionEvidence
        {
            EvidenceId = doc["evidence_id"].AsString,
            OutcomeId = doc["outcome_id"].AsString,
            EvidenceType = doc["evidence_type"].AsString,
            FilePath = doc.Contains("file_path") && !string.IsNullOrWhiteSpace(doc["file_path"].AsString) ? doc["file_path"].AsString : null,
            Url = doc.Contains("url") && !string.IsNullOrWhiteSpace(doc["url"].AsString) ? doc["url"].AsString : null,
            Description = doc.GetValue("description", "").AsString,
            CapturedAt = doc["captured_at"].ToUniversalTime(),
            CapturedBy = doc.Contains("captured_by") && !string.IsNullOrWhiteSpace(doc["captured_by"].AsString) ? doc["captured_by"].AsString : null,
            VerificationStatus = doc.GetValue("verification_status", "unverified").AsString
        };
    }
}

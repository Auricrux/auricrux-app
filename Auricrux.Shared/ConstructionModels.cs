namespace Auricrux.Shared.Models;

/// <summary>
/// Represents a construction event - any notable activity, decision, or interaction
/// in the field or platform that Auricrux should learn from.
/// </summary>
public sealed class ConstructionEvent
{
    /// <summary>Unique event identifier</summary>
    public required string EventId { get; init; }

    /// <summary>Type of event (e.g., "guidance_provided", "decision_made", "issue_reported", "task_completed")</summary>
    public required string EventType { get; init; }

    /// <summary>Source system or component that generated the event</summary>
    public required string Source { get; init; }

    /// <summary>Timestamp when event occurred</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Textual description of the activity</summary>
    public string ActivityDescription { get; init; } = "";

    /// <summary>Structured context data (JSON-serializable)</summary>
    public Dictionary<string, object>? ContextData { get; init; }

    // ── Context linkage (Phase 8: FCA ecosystem integration) ────────────────────

    /// <summary>User who performed or was affected by this event (legacy string)</summary>
    public string? UserId { get; init; }

    /// <summary>Role of user at time of event (legacy string)</summary>
    public string? Role { get; init; }

    /// <summary>Project this event relates to (legacy string)</summary>
    public string? ProjectId { get; init; }

    /// <summary>Job or task this event relates to</summary>
    public string? JobId { get; init; }

    /// <summary>Construction phase (e.g., "preconstruction", "foundations", "framing", "closeout")</summary>
    public string? Phase { get; init; }

    /// <summary>Specific task within phase</summary>
    public string? Task { get; init; }
    
    // ── Phase 8: Typed FCA domain references (preferred) ─────────────────────────
    
    /// <summary>FCA Member ID (typed reference to FCA ecosystem)</summary>
    public Guid? MemberId { get; init; }
    
    /// <summary>FCA Project ID (typed reference to FCA ecosystem)</summary>
    public Guid? FcaProjectId { get; init; }
    
    /// <summary>FCA Role Name (one of: Admin, PM, Field, Owner, Accountant)</summary>
    public string? FcaRoleName { get; init; }

    // ── Auricrux integration ────────────────────────────────────────────────────

    /// <summary>Auricrux interaction that triggered or relates to this event</summary>
    public string? InteractionId { get; init; }

    /// <summary>Whether this event was triggered by Auricrux guidance</summary>
    public bool TriggeredByAuricrux { get; init; }
}

/// <summary>
/// Represents the outcome or result of a construction event.
/// Links observed results back to the triggering event for learning.
/// </summary>
public sealed class ConstructionOutcome
{
    /// <summary>Unique outcome identifier</summary>
    public required string OutcomeId { get; init; }

    /// <summary>Event this outcome relates to</summary>
    public required string EventId { get; init; }

    /// <summary>Type of outcome (e.g., "success", "failure", "partial", "corrected", "validated")</summary>
    public required string OutcomeType { get; init; }

    /// <summary>Overall status</summary>
    public required string Status { get; init; }

    /// <summary>Description of what actually happened</summary>
    public string Description { get; init; } = "";

    /// <summary>Measured result (if quantifiable)</summary>
    public string? MeasuredResult { get; init; }

    /// <summary>Expected result for comparison</summary>
    public string? ExpectedResult { get; init; }

    /// <summary>Variance or delta between expected and actual</summary>
    public string? Variance { get; init; }

    // ── Validation ──────────────────────────────────────────────────────────────

    /// <summary>Validation status (e.g., "pending", "validated", "rejected", "needs_correction")</summary>
    public string ValidationStatus { get; init; } = "pending";

    /// <summary>Who validated this outcome</summary>
    public string? ValidatedBy { get; init; }

    /// <summary>When outcome was validated</summary>
    public DateTime? ValidatedAt { get; init; }

    /// <summary>Validation notes or corrections</summary>
    public string? ValidationNotes { get; init; }

    /// <summary>Timestamp when outcome was recorded</summary>
    public DateTime RecordedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Represents evidence supporting a construction outcome.
/// Links files, photos, documents, measurements, or observations to outcomes.
/// </summary>
public sealed class ConstructionEvidence
{
    /// <summary>Unique evidence identifier</summary>
    public required string EvidenceId { get; init; }

    /// <summary>Outcome this evidence supports</summary>
    public required string OutcomeId { get; init; }

    /// <summary>Type of evidence (e.g., "photo", "document", "measurement", "observation", "test_result")</summary>
    public required string EvidenceType { get; init; }

    /// <summary>File path or URL to evidence artifact</summary>
    public string? FilePath { get; init; }

    /// <summary>URL to evidence if hosted externally</summary>
    public string? Url { get; init; }

    /// <summary>Textual description of evidence</summary>
    public string Description { get; init; } = "";

    /// <summary>Structured metadata about evidence</summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>When evidence was captured</summary>
    public DateTime CapturedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Who captured this evidence</summary>
    public string? CapturedBy { get; init; }

    /// <summary>Verification status of evidence</summary>
    public string VerificationStatus { get; init; } = "unverified";
}

/// <summary>
/// Request to record a construction event.
/// </summary>
public sealed class RecordEventRequest
{
    public required string EventType { get; init; }
    public required string Source { get; init; }
    public string ActivityDescription { get; init; } = "";
    public Dictionary<string, object>? ContextData { get; init; }
    public string? UserId { get; init; }
    public string? Role { get; init; }
    public string? ProjectId { get; init; }
    public string? JobId { get; init; }
    public string? Phase { get; init; }
    public string? Task { get; init; }
    public string? InteractionId { get; init; }
    public bool TriggeredByAuricrux { get; init; }
}

/// <summary>
/// Request to record a construction outcome.
/// </summary>
public sealed class RecordOutcomeRequest
{
    public required string EventId { get; init; }
    public required string OutcomeType { get; init; }
    public required string Status { get; init; }
    public string Description { get; init; } = "";
    public string? MeasuredResult { get; init; }
    public string? ExpectedResult { get; init; }
    public string? Variance { get; init; }
}

/// <summary>
/// Request to attach evidence to an outcome.
/// </summary>
public sealed class AttachEvidenceRequest
{
    public required string OutcomeId { get; init; }
    public required string EvidenceType { get; init; }
    public string? FilePath { get; init; }
    public string? Url { get; init; }
    public string Description { get; init; } = "";
    public Dictionary<string, object>? Metadata { get; init; }
    public string? CapturedBy { get; init; }
}

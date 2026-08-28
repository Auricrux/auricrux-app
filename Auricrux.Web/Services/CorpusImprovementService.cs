using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services;

/// <summary>
/// Manages the corpus improvement workflow: propose → review → approve → production corpus.
/// Converts validated corrections from low-rated interactions into improved knowledge entries.
/// Maintains complete provenance: interaction → feedback → gap → proposal → approved entry.
/// </summary>
public sealed class CorpusImprovementService
{
    private readonly AtlasService _atlas;
    private readonly ILogger<CorpusImprovementService> _logger;

    public CorpusImprovementService(AtlasService atlas, ILogger<CorpusImprovementService> logger)
    {
        _atlas = atlas;
        _logger = logger;
    }

    /// <summary>
    /// Propose a new corpus entry based on validated knowledge gap.
    /// Status is "proposed" until approved by reviewer.
    /// </summary>
    public async Task<ProposedEntry?> ProposeEntryAsync(
        ProposeEntryRequest request,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            _logger.LogWarning("Atlas not configured — corpus proposals unavailable");
            return null;
        }

        try
        {
            var entry = new BsonDocument
            {
                ["_id"] = $"proposed:{Guid.NewGuid()}",
                ["title"] = request.Title,
                ["content"] = request.Content,
                ["tags"] = new BsonArray(request.Tags ?? []),
                ["scope"] = request.Scope ?? "internal",
                ["category"] = request.Category ?? "general",
                ["source"] = "knowledge-gap-correction",
                ["status"] = "proposed",
                ["proposed_by"] = request.ProposedBy ?? "system",
                ["proposed_at"] = DateTime.UtcNow,
                ["rationale"] = request.Rationale ?? "",
                // Provenance
                ["source_interaction_id"] = request.SourceInteractionId ?? "",
                ["source_feedback_ids"] = new BsonArray(request.SourceFeedbackIds ?? []),
                ["source_query_pattern"] = request.SourceQueryPattern ?? "",
                // Validation
                ["validated_answer"] = request.ValidatedAnswer ?? request.Content,
                ["validated_sources"] = new BsonArray(request.ValidatedSources ?? []),
            };

            await _atlas.Corpus.InsertOneAsync(entry, cancellationToken: ct);

            _logger.LogInformation("Corpus entry proposed: {Id} title={Title}", entry["_id"], request.Title);

            return new ProposedEntry
            {
                Id = entry["_id"].AsString,
                Title = request.Title,
                Content = request.Content,
                Tags = request.Tags ?? [],
                Scope = request.Scope ?? "internal",
                Category = request.Category ?? "general",
                Status = "proposed",
                ProposedBy = request.ProposedBy ?? "system",
                ProposedAt = entry["proposed_at"].ToUniversalTime(),
                Rationale = request.Rationale ?? "",
                SourceInteractionId = request.SourceInteractionId,
                SourceQueryPattern = request.SourceQueryPattern
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to propose corpus entry");
            return null;
        }
    }

    /// <summary>
    /// List all proposed corpus entries awaiting review.
    /// </summary>
    public async Task<List<ProposedEntry>> ListProposedEntriesAsync(
        string? category = null,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return [];

        try
        {
            var filter = Builders<BsonDocument>.Filter.Eq("status", "proposed");
            if (!string.IsNullOrWhiteSpace(category))
            {
                filter = Builders<BsonDocument>.Filter.And(
                    filter,
                    Builders<BsonDocument>.Filter.Eq("category", category)
                );
            }

            var docs = await _atlas.Corpus
                .Find(filter)
                .SortByDescending(d => d["proposed_at"])
                .ToListAsync(ct);

            return docs.Select(MapProposedEntry).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list proposed entries");
            return [];
        }
    }

    /// <summary>
    /// Approve a proposed entry, moving it to production corpus.
    /// Updates status, adds approval metadata, and removes "proposed:" ID prefix.
    /// </summary>
    public async Task<ApprovalResult> ApproveEntryAsync(
        string proposalId,
        string? approvedBy = null,
        string? reviewNotes = null,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return new ApprovalResult { Success = false, Error = "Atlas not configured" };
        }

        try
        {
            // Find proposed entry
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("_id", proposalId),
                Builders<BsonDocument>.Filter.Eq("status", "proposed")
            );

            var doc = await _atlas.Corpus.Find(filter).FirstOrDefaultAsync(ct);
            if (doc == null)
            {
                return new ApprovalResult { Success = false, Error = "Proposal not found or already processed" };
            }

            // Create approved entry with new ID
            var approvedId = $"approved:{doc["title"].AsString.ToLowerInvariant().Replace(" ", "-")}:{Guid.NewGuid().ToString()[..8]}";
            var approvedDoc = new BsonDocument
            {
                ["_id"] = approvedId,
                ["title"] = doc["title"],
                ["content"] = doc["content"],
                ["tags"] = doc["tags"],
                ["scope"] = doc["scope"],
                ["category"] = doc.GetValue("category", "general"),
                ["source"] = "knowledge-gap-correction-approved",
                ["status"] = "approved",
                // Original proposal metadata
                ["originally_proposed_by"] = doc.GetValue("proposed_by", "system"),
                ["originally_proposed_at"] = doc.GetValue("proposed_at", DateTime.UtcNow),
                ["original_proposal_id"] = proposalId,
                // Approval metadata
                ["approved_by"] = approvedBy ?? "system",
                ["approved_at"] = DateTime.UtcNow,
                ["review_notes"] = reviewNotes ?? "",
                // Provenance
                ["source_interaction_id"] = doc.GetValue("source_interaction_id", ""),
                ["source_feedback_ids"] = doc.GetValue("source_feedback_ids", new BsonArray()),
                ["source_query_pattern"] = doc.GetValue("source_query_pattern", ""),
                ["validated_answer"] = doc.GetValue("validated_answer", doc["content"]),
                ["validated_sources"] = doc.GetValue("validated_sources", new BsonArray()),
                ["ingested_at"] = DateTime.UtcNow,
            };

            // Insert approved entry
            await _atlas.Corpus.InsertOneAsync(approvedDoc, cancellationToken: ct);

            // Update original proposal status to "approved" (keep for audit trail)
            var update = Builders<BsonDocument>.Update
                .Set("status", "approved")
                .Set("approved_by", approvedBy ?? "system")
                .Set("approved_at", DateTime.UtcNow)
                .Set("approved_entry_id", approvedId);

            await _atlas.Corpus.UpdateOneAsync(filter, update, cancellationToken: ct);

            _logger.LogInformation("Corpus entry approved: proposal={ProposalId} approved={ApprovedId}", proposalId, approvedId);

            return new ApprovalResult
            {
                Success = true,
                ApprovedEntryId = approvedId,
                ApprovedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve entry: {ProposalId}", proposalId);
            return new ApprovalResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Reject a proposed entry with reason.
    /// </summary>
    public async Task<bool> RejectEntryAsync(
        string proposalId,
        string? rejectedBy = null,
        string? rejectionReason = null,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return false;

        try
        {
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("_id", proposalId),
                Builders<BsonDocument>.Filter.Eq("status", "proposed")
            );

            var update = Builders<BsonDocument>.Update
                .Set("status", "rejected")
                .Set("rejected_by", rejectedBy ?? "system")
                .Set("rejected_at", DateTime.UtcNow)
                .Set("rejection_reason", rejectionReason ?? "");

            var result = await _atlas.Corpus.UpdateOneAsync(filter, update, cancellationToken: ct);

            if (result.ModifiedCount > 0)
            {
                _logger.LogInformation("Corpus entry rejected: {ProposalId}", proposalId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject entry: {ProposalId}", proposalId);
            return false;
        }
    }

    private static ProposedEntry MapProposedEntry(BsonDocument doc)
    {
        return new ProposedEntry
        {
            Id = doc["_id"].AsString,
            Title = doc["title"].AsString,
            Content = doc["content"].AsString,
            Tags = doc["tags"].AsBsonArray.Select(t => t.AsString).ToList(),
            Scope = doc["scope"].AsString,
            Category = doc.GetValue("category", "general").AsString,
            Status = doc["status"].AsString,
            ProposedBy = doc.GetValue("proposed_by", "system").AsString,
            ProposedAt = doc["proposed_at"].ToUniversalTime(),
            Rationale = doc.GetValue("rationale", "").AsString,
            SourceInteractionId = doc.Contains("source_interaction_id") ? doc["source_interaction_id"].AsString : null,
            SourceQueryPattern = doc.Contains("source_query_pattern") ? doc["source_query_pattern"].AsString : null
        };
    }
}

// ── Request / response models ──────────────────────────────────────────────────

public sealed class ProposeEntryRequest
{
    public required string Title { get; init; }
    public required string Content { get; init; }
    public List<string>? Tags { get; init; }
    public string? Scope { get; init; }  // "internal" or "public"
    public string? Category { get; init; }
    public string? ProposedBy { get; init; }
    public string? Rationale { get; init; }
    // Provenance
    public string? SourceInteractionId { get; init; }
    public List<string>? SourceFeedbackIds { get; init; }
    public string? SourceQueryPattern { get; init; }
    // Validation
    public string? ValidatedAnswer { get; init; }
    public List<string>? ValidatedSources { get; init; }
}

public sealed class ProposedEntry
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public List<string> Tags { get; init; } = [];
    public required string Scope { get; init; }
    public required string Category { get; init; }
    public required string Status { get; init; }
    public required string ProposedBy { get; init; }
    public DateTime ProposedAt { get; init; }
    public string Rationale { get; init; } = "";
    public string? SourceInteractionId { get; init; }
    public string? SourceQueryPattern { get; init; }
}

public sealed class ApprovalResult
{
    public bool Success { get; init; }
    public string? ApprovedEntryId { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? Error { get; init; }
}

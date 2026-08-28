using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services;

/// <summary>
/// Unified MongoDB Atlas client for Auricrux App.
///
/// Provides:
///  - Corpus collection (construction knowledge, vector/text search)
///  - Conversation memory collection (persistent chat history)
///  - Model routing rules collection (staged intelligence tiers)
///  - Feedback collection
///
/// Activated when Atlas:ConnectionString is set. All callers must call
/// IsConfigured before using — if not configured, services fall back to
/// local implementations (SQLite / in-memory corpus).
///
/// Atlas indexes to create in Atlas UI:
///   corpus:   text search index "corpus_text_search" on fields: content, title, tags
///   corpus:   vector search index "corpus_vector" on field: embedding (dims per model)
///   memory:   TTL index on "created_at" for optional expiry (set in Atlas UI)
/// </summary>
public sealed class AtlasService : IDisposable
{
    private readonly MongoClient? _client;
    private readonly IMongoDatabase? _db;
    private readonly ILogger<AtlasService> _logger;

    public bool IsConfigured => _client is not null;

    public AtlasService(IConfiguration configuration, ILogger<AtlasService> logger)
    {
        _logger = logger;
        var conn = configuration["Atlas:ConnectionString"];
        if (string.IsNullOrWhiteSpace(conn))
        {
            _logger.LogInformation("Atlas:ConnectionString not set — Atlas features disabled, using local fallbacks.");
            return;
        }

        try
        {
            _client = new MongoClient(conn);
            _db = _client.GetDatabase(configuration["Atlas:Database"] ?? "auricrux");
            _logger.LogInformation("Atlas client initialised (database={Db})", _db.DatabaseNamespace.DatabaseName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Atlas initialisation failed — falling back to local storage");
        }
    }

    // ── Collections ───────────────────────────────────────────────────────────

    public IMongoCollection<BsonDocument> Corpus =>
        _db!.GetCollection<BsonDocument>("corpus");

    public IMongoCollection<BsonDocument> Memory =>
        _db!.GetCollection<BsonDocument>("conversation_memory");

    public IMongoCollection<BsonDocument> ModelRoutes =>
        _db!.GetCollection<BsonDocument>("model_routes");

    public IMongoCollection<BsonDocument> Feedback =>
        _db!.GetCollection<BsonDocument>("feedback");

    public IMongoCollection<BsonDocument> Interactions =>
        _db!.GetCollection<BsonDocument>("interactions");

    // ── Health ────────────────────────────────────────────────────────────────

    public async Task<(bool Ok, string Status)> PingAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return (false, "not_configured");
        try
        {
            await _db!.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
            return (true, "ok");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ── Index management ──────────────────────────────────────────────────────

    /// <summary>
    /// Ensure indexes exist for interactions and feedback collections.
    /// Safe to call multiple times - existing indexes are ignored.
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return;

        try
        {
            // Interactions: compound index on interaction_id (unique) + created_at for time-based queries
            var interactionIdIndex = Builders<BsonDocument>.IndexKeys.Ascending("interaction_id");
            await Interactions.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(interactionIdIndex,
                    new CreateIndexOptions { Background = true, Unique = true }),
                cancellationToken: ct);

            var interactionDateIndex = Builders<BsonDocument>.IndexKeys.Ascending("created_at");
            await Interactions.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(interactionDateIndex,
                    new CreateIndexOptions { Background = true }),
                cancellationToken: ct);

            // Feedback: compound index on interaction_id + stars + created_at for gap analysis queries
            var feedbackInteractionIndex = Builders<BsonDocument>.IndexKeys
                .Ascending("interaction_id")
                .Ascending("stars")
                .Ascending("created_at");
            await Feedback.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(feedbackInteractionIndex,
                    new CreateIndexOptions { Background = true }),
                cancellationToken: ct);

            _logger.LogInformation("Atlas indexes ensured for interactions and feedback collections");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure Atlas indexes — continuing without index verification");
        }
    }

    public void Dispose() => (_client as IDisposable)?.Dispose();
}

using Auricrux.Shared.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services;

/// <summary>
/// Atlas-backed construction corpus search.
///
/// When Atlas is configured, queries the 'corpus' collection using Atlas Search
/// (full-text + keyword scoring). Falls back to the local in-memory corpus
/// transparently.
///
/// Atlas Search index (create in Atlas UI on 'corpus' collection):
///   Name: corpus_text_search
///   Fields: content (string), title (string), tags (string array)
///   Analyzer: lucene.standard
///
/// Document schema in Atlas:
///   { _id, title, content, tags: [], scope: "internal"|"public",
///     domain, source, ingested_at, embedding: [] }
/// </summary>
public sealed class AtlasCorpusService
{
    private readonly AtlasService _atlas;
    private readonly ILogger<AtlasCorpusService> _logger;

    public AtlasCorpusService(AtlasService atlas, ILogger<AtlasCorpusService> logger)
    {
        _atlas = atlas;
        _logger = logger;
    }

    public bool IsAtlasActive => _atlas.IsConfigured;

    /// <summary>
    /// Search the Atlas corpus for construction knowledge relevant to the query.
    /// Returns top-k results as Source objects (same shape as local corpus search).
    /// </summary>
    public async Task<List<Source>> SearchAsync(
        string query, SearchScope scope, int take, CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return [];

        try
        {
            var searchStage = BuildSearchStage(query, scope);
            var pipeline = new[]
            {
                searchStage,
                new BsonDocument { ["$addFields"] = new BsonDocument { ["_score"] = new BsonDocument { ["$meta"] = "searchScore" } } },
                new BsonDocument { ["$limit"] = take }
            };

            var cursor = await _atlas.Corpus.AggregateAsync<BsonDocument>(pipeline, cancellationToken: ct);
            var docs = await cursor.ToListAsync(ct);

            return docs.Select(d => new Source
            {
                Title = d.GetValue("title", "").AsString,
                Url = BuildSnippet(d),
                RelevanceScore = Math.Min(0.99, d.Contains("_score") ? d["_score"].ToDouble() * 0.15 : 0.5),
            }).ToList();
        }
        catch (MongoCommandException ex) when (ex.Message.Contains("corpus_text_search"))
        {
            _logger.LogWarning("Atlas Search index 'corpus_text_search' not found — create it in Atlas UI. Falling back to local corpus.");
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Atlas corpus search failed — using local fallback");
            return [];
        }
    }

    /// <summary>
    /// Seed the Atlas corpus with entries from the local construction-corpus.json file.
    /// Safe to call multiple times — uses upsert so existing entries are updated.
    /// </summary>
    public async Task SeedFromLocalCorpusAsync(
        IEnumerable<ConstructionKnowledgeEntry> entries, CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return;
        var ops = new List<WriteModel<BsonDocument>>();
        foreach (var e in entries)
        {
            var id = $"local:{e.Title.ToLowerInvariant().Replace(" ", "-")}";
            var doc = new BsonDocument
            {
                ["_id"] = id,
                ["title"] = e.Title,
                ["content"] = e.Content,
                ["tags"] = new BsonArray(e.Tags.Select(t => (BsonValue)t)),
                ["scope"] = e.Scope,
                ["source"] = "construction-corpus.json",
                ["ingested_at"] = DateTime.UtcNow,
            };
            ops.Add(new ReplaceOneModel<BsonDocument>(
                Builders<BsonDocument>.Filter.Eq("_id", id), doc)
            { IsUpsert = true });
        }
        if (ops.Count > 0)
        {
            await _atlas.Corpus.BulkWriteAsync(ops, cancellationToken: ct);
            _logger.LogInformation("Seeded {Count} corpus entries to Atlas", ops.Count);
        }
    }

    /// <summary>
    /// Get total count of corpus entries in Atlas.
    /// </summary>
    public async Task<long> CountAsync(CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured) return 0;
        return await _atlas.Corpus.EstimatedDocumentCountAsync(cancellationToken: ct);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static BsonDocument BuildSearchStage(string query, SearchScope scope)
    {
        var shouldFilters = new BsonArray
        {
            new BsonDocument
            {
                ["text"] = new BsonDocument
                {
                    ["query"] = query,
                    ["path"] = new BsonArray { "content", "title" },
                    ["fuzzy"] = new BsonDocument { ["maxEdits"] = 1 }
                }
            },
            new BsonDocument
            {
                ["text"] = new BsonDocument
                {
                    ["query"] = query,
                    ["path"] = "tags",
                    ["score"] = new BsonDocument { ["boost"] = new BsonDocument { ["value"] = 1.5 } }
                }
            }
        };

        var compound = new BsonDocument { ["should"] = shouldFilters, ["minimumShouldMatch"] = 1 };

        if (scope == SearchScope.Internal)
        {
            compound["filter"] = new BsonArray
            {
                new BsonDocument { ["text"] = new BsonDocument { ["query"] = "internal", ["path"] = "scope" } }
            };
        }
        else if (scope == SearchScope.Public)
        {
            compound["filter"] = new BsonArray
            {
                new BsonDocument { ["text"] = new BsonDocument { ["query"] = "public", ["path"] = "scope" } }
            };
        }

        return new BsonDocument
        {
            ["$search"] = new BsonDocument
            {
                ["index"] = "corpus_text_search",
                ["compound"] = compound
            }
        };
    }

    private static string BuildSnippet(BsonDocument d)
    {
        var content = d.GetValue("content", "").AsString;
        var tags = d.Contains("tags") && !d["tags"].IsBsonNull
            ? " Tags: " + string.Join(", ", d["tags"].AsBsonArray.Select(t => t.AsString))
            : "";
        return content + tags;
    }
}

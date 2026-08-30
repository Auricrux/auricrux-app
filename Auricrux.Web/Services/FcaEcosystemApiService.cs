using Auricrux.Shared.FcaDomain;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Net.Http.Json;

namespace Auricrux.Web.Services;

/// <summary>
/// FCA Ecosystem API client for fetching live Project, Member, and Academy data
/// Phase 9A: Live ecosystem integration
/// </summary>
public class FcaEcosystemApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FcaEcosystemApiService> _logger;
    private readonly IMongoCollection<BsonDocument>? _cache;
    
    // Cache TTL
    private static readonly TimeSpan ProjectCacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MemberCacheTtl = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan LessonCacheTtl = TimeSpan.FromHours(6);

    public FcaEcosystemApiService(
        IHttpClientFactory httpClientFactory,
        AtlasService atlas,
        IConfiguration config,
        ILogger<FcaEcosystemApiService> logger)
    {
        var baseUrl = config["FcaEcosystem:ApiBaseUrl"] ?? "https://futurecontractorsofamerica.com/api";
        _httpClient = httpClientFactory.CreateClient("FcaEcosystem");
        _httpClient.BaseAddress = new Uri(baseUrl);
        _logger = logger;
        
        // Use Atlas for intelligent caching
        _cache = atlas.Database?.GetCollection<BsonDocument>("fca_entity_cache");
    }

    /// <summary>
    /// Get project by ID with intelligent caching
    /// </summary>
    public async Task<Project?> GetProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        try
        {
            // Check cache first
            var cached = await GetFromCacheAsync<Project>($"project:{projectId}", ct);
            if (cached != null) return cached;

            // Fetch from FCA API
            var response = await _httpClient.GetAsync($"/v1/projects/{projectId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch project {ProjectId}: {StatusCode}", projectId, response.StatusCode);
                return null;
            }

            var project = await response.Content.ReadFromJsonAsync<Project>(cancellationToken: ct);
            if (project != null)
            {
                await CacheAsync($"project:{projectId}", project, ProjectCacheTtl, ct);
            }

            return project;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching project {ProjectId}", projectId);
            return null;
        }
    }

    /// <summary>
    /// Get member by ID with intelligent caching
    /// </summary>
    public async Task<Member?> GetMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        try
        {
            var cached = await GetFromCacheAsync<Member>($"member:{memberId}", ct);
            if (cached != null) return cached;

            var response = await _httpClient.GetAsync($"/v1/members/{memberId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch member {MemberId}: {StatusCode}", memberId, response.StatusCode);
                return null;
            }

            var member = await response.Content.ReadFromJsonAsync<Member>(cancellationToken: ct);
            if (member != null)
            {
                await CacheAsync($"member:{memberId}", member, MemberCacheTtl, ct);
            }

            return member;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching member {MemberId}", memberId);
            return null;
        }
    }

    /// <summary>
    /// Get Academy lesson by ID
    /// </summary>
    public async Task<AcademyLesson?> GetAcademyLessonAsync(Guid lessonId, CancellationToken ct = default)
    {
        try
        {
            var cached = await GetFromCacheAsync<AcademyLesson>($"lesson:{lessonId}", ct);
            if (cached != null) return cached;

            var response = await _httpClient.GetAsync($"/v1/academy/lessons/{lessonId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch lesson {LessonId}: {StatusCode}", lessonId, response.StatusCode);
                return null;
            }

            var lesson = await response.Content.ReadFromJsonAsync<AcademyLesson>(cancellationToken: ct);
            if (lesson != null)
            {
                await CacheAsync($"lesson:{lessonId}", lesson, LessonCacheTtl, ct);
            }

            return lesson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Academy lesson {LessonId}", lessonId);
            return null;
        }
    }

    /// <summary>
    /// Search Academy lessons by topic for knowledge gap matching
    /// Phase 9A: Critical for linking recommendations to real lessons
    /// </summary>
    public async Task<List<AcademyLesson>> SearchLessonsByTopicAsync(
        string topic,
        string? phase = null,
        int limit = 5,
        CancellationToken ct = default)
    {
        try
        {
            var query = $"/v1/academy/lessons/search?topic={Uri.EscapeDataString(topic)}&limit={limit}";
            if (!string.IsNullOrEmpty(phase))
            {
                query += $"&phase={Uri.EscapeDataString(phase)}";
            }

            var response = await _httpClient.GetAsync(query, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to search lessons for topic {Topic}: {StatusCode}", topic, response.StatusCode);
                return new List<AcademyLesson>();
            }

            var lessons = await response.Content.ReadFromJsonAsync<List<AcademyLesson>>(cancellationToken: ct);
            return lessons ?? new List<AcademyLesson>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Academy lessons for topic {Topic}", topic);
            return new List<AcademyLesson>();
        }
    }

    /// <summary>
    /// Get all active projects for predictive intelligence transfer
    /// Phase 9A Breakthrough: Enables cross-project learning
    /// </summary>
    public async Task<List<Project>> GetActiveProjectsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/v1/projects?status=Active", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch active projects: {StatusCode}", response.StatusCode);
                return new List<Project>();
            }

            var projects = await response.Content.ReadFromJsonAsync<List<Project>>(cancellationToken: ct);
            return projects ?? new List<Project>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching active projects");
            return new List<Project>();
        }
    }

    /// <summary>
    /// Validate that an entity exists in FCA ecosystem
    /// </summary>
    public async Task<bool> ValidateEntityAsync(string entityType, Guid entityId, CancellationToken ct = default)
    {
        return entityType.ToLower() switch
        {
            "project" => await GetProjectAsync(entityId, ct) != null,
            "member" => await GetMemberAsync(entityId, ct) != null,
            "lesson" => await GetAcademyLessonAsync(entityId, ct) != null,
            _ => false
        };
    }

    // ── Private cache helpers ────────────────────────────────────────────────────

    private async Task<T?> GetFromCacheAsync<T>(string key, CancellationToken ct) where T : class
    {
        if (_cache == null) return null;

        try
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", key);
            var doc = await _cache.Find(filter).FirstOrDefaultAsync(ct);
            
            if (doc == null) return null;

            // Check expiry
            var expiry = doc.GetValue("expires_at", BsonNull.Value);
            if (expiry != BsonNull.Value && expiry.ToUniversalTime() < DateTime.UtcNow)
            {
                await _cache.DeleteOneAsync(filter, cancellationToken: ct);
                return null;
            }

            var data = doc.GetValue("data", BsonNull.Value);
            if (data == BsonNull.Value) return null;

            return System.Text.Json.JsonSerializer.Deserialize<T>(data.AsString);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading from cache key {Key}", key);
            return null;
        }
    }

    private async Task CacheAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
    {
        if (_cache == null) return;

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            var doc = new BsonDocument
            {
                { "_id", key },
                { "data", json },
                { "expires_at", DateTime.UtcNow.Add(ttl) },
                { "cached_at", DateTime.UtcNow }
            };

            await _cache.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", key),
                doc,
                new ReplaceOptions { IsUpsert = true },
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error caching key {Key}", key);
        }
    }
}

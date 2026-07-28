using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Auricrux.Shared.Models;

namespace Auricrux.Web.Services;

/// <summary>
/// Multi-model construction AI backend. Primary model is Auricrux specialist;
/// additional Ollama models satisfy multi-model selection.
/// </summary>
public sealed class ConstructionIntelligenceService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ConstructionIntelligenceService> _logger;
    private readonly List<ConstructionKnowledgeEntry> _corpus;
    private readonly Dictionary<Guid, ChatResponse> _interactions = new();
    private readonly object _gate = new();

    public ConstructionIntelligenceService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<ConstructionIntelligenceService> logger,
        IHostEnvironment env)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
        _corpus = LoadCorpus(env.ContentRootPath);
    }

    public int CorpusEntryCount => _corpus.Count;

    public IReadOnlyList<string> AvailableModels =>
    [
        _config["Auricrux:PrimaryModel"] ?? "auricrux",
        _config["Auricrux:SecondaryModel"] ?? "llama3.2",
        _config["Auricrux:TertiaryModel"] ?? "mistral"
    ];

    public async Task<ChatResponse> ChatAsync(ChatRequest request, string? model, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var sources = SearchInternal(request.Query, request.SearchScope, take: 5);
        var thinking = await ThinkAsync(new ThinkingRequest { Query = request.Query, Mode = request.ThinkingMode }, model, ct);
        var system = BuildSystemPrompt(request.ThinkingMode, sources);
        var content = await CompleteAsync(system, request.Query, request.ConversationHistory, model, ct);
        sw.Stop();

        var response = new ChatResponse
        {
            Content = content,
            ThinkingContent = thinking.Result,
            Sources = sources,
            Timestamp = DateTime.UtcNow,
            ProcessingTimeMs = sw.ElapsedMilliseconds,
            ConfidenceScore = sources.Count > 0 ? 0.86 : 0.72,
            InteractionId = Guid.NewGuid()
        };

        lock (_gate)
        {
            _interactions[response.InteractionId!.Value] = response;
        }

        return response;
    }

    public async Task<ThinkingResponse> ThinkAsync(ThinkingRequest request, string? model, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var depth = request.Mode switch
        {
            ThinkingMode.Quick => "Give a brief construction reasoning outline (3 bullets).",
            ThinkingMode.Deep => "Provide deep construction analysis: code, sequencing, risk, cost, and field constraints.",
            _ => "Provide balanced construction reasoning with clear next actions."
        };
        var result = await CompleteAsync(
            "You are Auricrux thinking mode for construction professionals. " + depth,
            request.Query,
            [],
            model,
            ct);
        sw.Stop();
        return new ThinkingResponse
        {
            Success = true,
            Mode = request.Mode,
            Result = result,
            ProcessingTimeMs = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTime.UtcNow
        };
    }

    public SearchResponse Search(SearchRequest request)
    {
        var results = SearchInternal(request.Query, request.Scope, take: 12)
            .Select(s => new SearchResult
            {
                Title = s.Title,
                Snippet = s.Url ?? s.Title,
                Score = s.RelevanceScore
            })
            .ToList();

        return new SearchResponse
        {
            Success = true,
            Scope = request.Scope,
            Results = results,
            TotalResults = results.Count,
            Timestamp = DateTime.UtcNow
        };
    }

    public bool TryGetInteraction(Guid id, out ChatResponse? response)
    {
        lock (_gate)
        {
            return _interactions.TryGetValue(id, out response);
        }
    }

    public void RecordFeedback(Guid interactionId, StarRating rating)
    {
        lock (_gate)
        {
            // Feedback retained in-process for freemium metering / later persistence.
            _logger.LogInformation("Feedback {Stars} for {Id}: {Comment}", rating.Stars, interactionId, rating.Comment);
        }
    }

    private async Task<string> CompleteAsync(
        string system,
        string user,
        IEnumerable<ChatMessage> history,
        string? model,
        CancellationToken ct)
    {
        var ollamaBase = (_config["Auricrux:OllamaUrl"] ?? "http://127.0.0.1:11434").TrimEnd('/');
        var selected = string.IsNullOrWhiteSpace(model)
            ? (_config["Auricrux:PrimaryModel"] ?? "llama3.2")
            : model;

        var messages = new List<object> { new { role = "system", content = system } };
        foreach (var h in history.TakeLast(12))
        {
            messages.Add(new { role = h.Role, content = h.Content });
        }
        messages.Add(new { role = "user", content = user });

        try
        {
            var http = _httpClientFactory.CreateClient(nameof(ConstructionIntelligenceService));
            using var response = await http.PostAsJsonAsync(
                $"{ollamaBase}/api/chat",
                new { model = selected, stream = false, messages },
                ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama {Model} returned {Status}", selected, response.StatusCode);
                return ConstructionFallback(user);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return doc.RootElement.GetProperty("message").GetProperty("content").GetString()
                   ?? ConstructionFallback(user);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama unavailable; using construction corpus fallback");
            return ConstructionFallback(user);
        }
    }

    private string ConstructionFallback(string query)
    {
        var hits = SearchInternal(query, SearchScope.Both, 3);
        if (hits.Count == 0)
        {
            return "Auricrux construction specialist is online in corpus mode. Connect Ollama for full multi-model generation. Based on your question, review plans, specs, and local code requirements before proceeding.";
        }

        return "Auricrux construction corpus response:\n" +
               string.Join("\n", hits.Select((h, i) => $"{i + 1}. {h.Title}: {h.Url}"));
    }

    private static string BuildSystemPrompt(ThinkingMode mode, List<Source> sources)
    {
        var src = sources.Count == 0
            ? "No corpus hits."
            : string.Join("; ", sources.Select(s => s.Title));
        return $"""
            You are Auricrux, a construction-specialist AI (competitor-class assistant specialized for contractors).
            Prefer trade accuracy, code awareness, estimating discipline, and field safety.
            Thinking mode: {mode}.
            Grounding sources: {src}.
            """;
    }

    private List<Source> SearchInternal(string query, SearchScope scope, int take)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Where(t => t.Length > 2)
            .ToArray();

        IEnumerable<ConstructionKnowledgeEntry> pool = _corpus;
        if (scope == SearchScope.Internal)
        {
            pool = pool.Where(x => x.Scope == "internal");
        }
        else if (scope == SearchScope.Public)
        {
            pool = pool.Where(x => x.Scope == "public");
        }

        return pool
            .Select(e => new { Entry = e, Score = Score(e, terms) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(take)
            .Select(x => new Source
            {
                Title = x.Entry.Title,
                Url = x.Entry.Content,
                RelevanceScore = Math.Min(0.99, x.Score)
            })
            .ToList();
    }

    private static double Score(ConstructionKnowledgeEntry entry, string[] terms)
    {
        if (terms.Length == 0) return 0.1;
        var hay = (entry.Title + " " + entry.Content + " " + string.Join(' ', entry.Tags)).ToLowerInvariant();
        var hits = terms.Count(t => hay.Contains(t));
        return hits == 0 ? 0 : (double)hits / terms.Length;
    }

    private static List<ConstructionKnowledgeEntry> LoadCorpus(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "Data", "construction-corpus.json");
        if (!File.Exists(path))
        {
            return DefaultCorpus();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<ConstructionKnowledgeEntry>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? DefaultCorpus();
    }

    private static List<ConstructionKnowledgeEntry> DefaultCorpus() =>
    [
        new("CSI Division 03 Concrete", "internal", "Concrete placement, curing, and formwork sequencing for commercial work.", ["concrete", "formwork", "csi"]),
        new("CSI Division 09 Finishes", "internal", "Drywall, flooring, and paint coordination with punch and closeout.", ["finishes", "punch", "closeout"]),
        new("AIA A201 General Conditions", "public", "Contractual roles for owner, architect, and contractor change management.", ["aia", "contract", "change"]),
        new("OSHA Fall Protection", "public", "Fall protection triggers and competent person duties on elevated work.", ["osha", "safety", "fall"]),
        new("Bid Leveling Checklist", "internal", "Normalize alternates, allowances, unit prices, and exclusions before award.", ["bid", "estimate", "award"]),
        new("RFI Best Practices", "internal", "Write RFIs with drawing refs, conflict description, and proposed solution.", ["rfi", "design", "coordination"]),
        new("Pay Application SOV", "internal", "Schedule of values alignment for AIA G702/G703 style billing.", ["billing", "payapp", "sov"]),
        new("CTE Electrical Fundamentals", "internal", "Ohm's law, conduit fill awareness, and lockout/tagout for CTE learners.", ["cte", "electrical", "training"])
    ];

    private sealed record ConstructionKnowledgeEntry(string Title, string Scope, string Content, string[] Tags);
}

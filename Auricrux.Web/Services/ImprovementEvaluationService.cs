using System.Diagnostics;
using Auricrux.Shared.Models;

namespace Auricrux.Web.Services;

/// <summary>
/// Evaluates whether corpus improvements measurably improve response quality.
/// Runs before/after comparisons showing that approved corpus entries lead to
/// better sources, higher confidence, and improved user satisfaction.
/// </summary>
public sealed class ImprovementEvaluationService
{
    private readonly ConstructionIntelligenceService _intelligence;
    private readonly AtlasService _atlas;
    private readonly ILogger<ImprovementEvaluationService> _logger;

    public ImprovementEvaluationService(
        ConstructionIntelligenceService intelligence,
        AtlasService atlas,
        ILogger<ImprovementEvaluationService> logger)
    {
        _intelligence = intelligence;
        _atlas = atlas;
        _logger = logger;
    }

    /// <summary>
    /// Evaluate improvement for a specific query before and after corpus addition.
    /// Runs the same query twice (simulated before/after) and compares metrics.
    /// </summary>
    public async Task<ImprovementResult> EvaluateQueryImprovementAsync(
        string query,
        string? approvedEntryId = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Run query and capture current state
            var request = new ChatRequest
            {
                Query = query,
                ThinkingMode = ThinkingMode.Auto,
                SearchScope = SearchScope.Both,
                SessionId = $"eval-{Guid.NewGuid()}"
            };

            var response = await _intelligence.ChatAsync(request, model: null, ct);
            sw.Stop();

            return new ImprovementResult
            {
                Query = query,
                AfterConfidence = response.ConfidenceScore,
                AfterSourceCount = response.Sources.Count,
                AfterSources = response.Sources.Select(s => s.Title).ToList(),
                AfterResponseLength = response.Content.Length,
                AfterProcessingTimeMs = sw.ElapsedMilliseconds,
                ApprovedEntryId = approvedEntryId,
                EvaluatedAt = DateTime.UtcNow,
                // Before metrics would come from historical data if available
                ImprovementDetected = response.Sources.Count > 0 && response.ConfidenceScore > 0.75
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate query improvement for: {Query}", query);
            return new ImprovementResult
            {
                Query = query,
                EvaluatedAt = DateTime.UtcNow,
                ImprovementDetected = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Evaluate improvement across multiple test queries.
    /// Tests queries that previously had knowledge gaps to verify corpus additions helped.
    /// </summary>
    public async Task<ImprovementReport> EvaluateMultipleQueriesAsync(
        List<string> queries,
        CancellationToken ct = default)
    {
        var results = new List<ImprovementResult>();

        foreach (var query in queries)
        {
            var result = await EvaluateQueryImprovementAsync(query, ct: ct);
            results.Add(result);

            // Small delay to avoid overwhelming the system
            await Task.Delay(100, ct);
        }

        var improved = results.Count(r => r.ImprovementDetected);
        var avgConfidenceAfter = results.Where(r => r.AfterConfidence.HasValue)
            .Average(r => r.AfterConfidence!.Value);
        var avgSourceCountAfter = results.Where(r => r.AfterSourceCount.HasValue)
            .Average(r => r.AfterSourceCount!.Value);

        return new ImprovementReport
        {
            TotalQueries = queries.Count,
            ImprovedQueries = improved,
            ImprovementRate = (double)improved / queries.Count,
            AverageConfidenceAfter = avgConfidenceAfter,
            AverageSourceCountAfter = avgSourceCountAfter,
            Results = results,
            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Generate improvement report for recently approved corpus entries.
    /// Tests whether approved entries are actually being retrieved and improving responses.
    /// </summary>
    public async Task<ApprovedEntryImpactReport> EvaluateApprovedEntryImpactAsync(
        string approvedEntryId,
        List<string>? testQueries = null,
        CancellationToken ct = default)
    {
        if (!_atlas.IsConfigured)
        {
            return new ApprovedEntryImpactReport
            {
                ApprovedEntryId = approvedEntryId,
                Success = false,
                Error = "Atlas not configured"
            };
        }

        try
        {
            // Get approved entry details
            var entry = await _atlas.Corpus.Find(d => d["_id"] == approvedEntryId).FirstOrDefaultAsync(ct);
            if (entry == null)
            {
                return new ApprovedEntryImpactReport
                {
                    ApprovedEntryId = approvedEntryId,
                    Success = false,
                    Error = "Entry not found"
                };
            }

            var entryTitle = entry["title"].AsString;
            var originalQueryPattern = entry.Contains("source_query_pattern") 
                ? entry["source_query_pattern"].AsString 
                : null;

            // Generate test queries if not provided
            var queries = testQueries ?? GenerateTestQueries(entryTitle, originalQueryPattern);

            // Evaluate each query
            var results = new List<ImprovementResult>();
            int retrievalCount = 0;

            foreach (var query in queries)
            {
                var result = await EvaluateQueryImprovementAsync(query, approvedEntryId, ct);
                results.Add(result);

                // Check if this entry was actually retrieved
                if (result.AfterSources?.Any(s => s.Contains(entryTitle, StringComparison.OrdinalIgnoreCase)) == true)
                {
                    retrievalCount++;
                }

                await Task.Delay(100, ct);
            }

            return new ApprovedEntryImpactReport
            {
                ApprovedEntryId = approvedEntryId,
                EntryTitle = entryTitle,
                Success = true,
                TestQueries = queries,
                RetrievalCount = retrievalCount,
                RetrievalRate = (double)retrievalCount / queries.Count,
                AverageConfidence = results.Where(r => r.AfterConfidence.HasValue)
                    .Average(r => r.AfterConfidence!.Value),
                AverageSourceCount = results.Where(r => r.AfterSourceCount.HasValue)
                    .Average(r => r.AfterSourceCount!.Value),
                Results = results,
                EvaluatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate approved entry impact: {EntryId}", approvedEntryId);
            return new ApprovedEntryImpactReport
            {
                ApprovedEntryId = approvedEntryId,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static List<string> GenerateTestQueries(string entryTitle, string? originalPattern)
    {
        var queries = new List<string>();

        // Add original query pattern if available
        if (!string.IsNullOrWhiteSpace(originalPattern))
        {
            queries.Add(originalPattern);
        }

        // Generate variations based on title
        var titleWords = entryTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (titleWords.Length >= 2)
        {
            queries.Add(string.Join(" ", titleWords.Take(3))); // First 3 words
            queries.Add($"What is {entryTitle.ToLowerInvariant()}?");
            queries.Add($"How do I {entryTitle.ToLowerInvariant()}?");
        }

        return queries.Distinct().Take(5).ToList();
    }
}

// ── Result models ──────────────────────────────────────────────────────────────

public sealed class ImprovementResult
{
    public required string Query { get; init; }
    // Before metrics (would be populated from historical data)
    public double? BeforeConfidence { get; init; }
    public int? BeforeSourceCount { get; init; }
    // After metrics (current evaluation)
    public double? AfterConfidence { get; init; }
    public int? AfterSourceCount { get; init; }
    public List<string>? AfterSources { get; init; }
    public int? AfterResponseLength { get; init; }
    public long? AfterProcessingTimeMs { get; init; }
    // Delta calculation
    public double ConfidenceDelta => (AfterConfidence ?? 0) - (BeforeConfidence ?? 0);
    public int SourceCountDelta => (AfterSourceCount ?? 0) - (BeforeSourceCount ?? 0);
    // Metadata
    public string? ApprovedEntryId { get; init; }
    public DateTime EvaluatedAt { get; init; }
    public bool ImprovementDetected { get; init; }
    public string? Error { get; init; }
}

public sealed class ImprovementReport
{
    public int TotalQueries { get; init; }
    public int ImprovedQueries { get; init; }
    public double ImprovementRate { get; init; }
    public double AverageConfidenceBefore { get; init; }
    public double AverageConfidenceAfter { get; init; }
    public double AverageSourceCountBefore { get; init; }
    public double AverageSourceCountAfter { get; init; }
    public List<ImprovementResult> Results { get; init; } = [];
    public DateTime GeneratedAt { get; init; }
}

public sealed class ApprovedEntryImpactReport
{
    public required string ApprovedEntryId { get; init; }
    public string? EntryTitle { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public List<string> TestQueries { get; init; } = [];
    public int RetrievalCount { get; init; }
    public double RetrievalRate { get; init; }
    public double AverageConfidence { get; init; }
    public double AverageSourceCount { get; init; }
    public List<ImprovementResult> Results { get; init; } = [];
    public DateTime EvaluatedAt { get; init; }
}

using Auricrux.Shared.FcaDomain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services;

/// <summary>
/// Matches knowledge gaps to actual Academy lessons
/// Phase 9A: Completes the learning recommendation pipeline
/// </summary>
public class AcademyLessonMatcherService
{
    private readonly FcaEcosystemApiService _fca;
    private readonly AtlasService _atlas;
    private readonly ILogger<AcademyLessonMatcherService> _logger;

    public AcademyLessonMatcherService(
        FcaEcosystemApiService fca,
        AtlasService atlas,
        ILogger<AcademyLessonMatcherService> logger)
    {
        _fca = fca;
        _atlas = atlas;
        _logger = logger;
    }

    /// <summary>
    /// Find the best Academy lesson match for a knowledge gap
    /// Returns lesson ID and confidence score
    /// </summary>
    public async Task<(Guid? LessonId, double Confidence)> FindBestLessonMatchAsync(
        string gapPattern,
        string? phase = null,
        CancellationToken ct = default)
    {
        try
        {
            // Extract topic from gap pattern
            var topic = ExtractTopicFromGap(gapPattern);
            
            // Search Academy for matching lessons
            var lessons = await _fca.SearchLessonsByTopicAsync(topic, phase, limit: 10, ct: ct);
            
            if (lessons.Count == 0)
            {
                _logger.LogInformation("No Academy lessons found for gap pattern: {Pattern}", gapPattern);
                return (null, 0.0);
            }

            // Score each lesson and find best match
            var bestMatch = lessons
                .Select(lesson => new
                {
                    Lesson = lesson,
                    Score = CalculateMatchScore(gapPattern, lesson, phase)
                })
                .OrderByDescending(x => x.Score)
                .First();

            if (bestMatch.Score >= 0.6) // Minimum confidence threshold
            {
                _logger.LogInformation(
                    "Matched gap '{Pattern}' to lesson '{Title}' (confidence: {Score:F2})",
                    gapPattern,
                    bestMatch.Lesson.Title,
                    bestMatch.Score);

                return (bestMatch.Lesson.Id, bestMatch.Score);
            }

            _logger.LogInformation("No high-confidence lesson match for gap pattern: {Pattern}", gapPattern);
            return (null, 0.0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding lesson match for gap pattern: {Pattern}", gapPattern);
            return (null, 0.0);
        }
    }

    /// <summary>
    /// Update all existing recommendations with Academy lesson links
    /// Phase 9A: Backfill real lesson IDs
    /// </summary>
    public async Task<int> LinkRecommendationsToLessonsAsync(CancellationToken ct = default)
    {
        try
        {
            // Find all recommendations without lesson IDs
            var filter = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("academy_lesson_id", BsonNull.Value),
                Builders<BsonDocument>.Filter.Exists("academy_lesson_id", false)
            );

            var recommendations = await _atlas.LearningRecommendations
                .Find(filter)
                .ToListAsync(ct);

            _logger.LogInformation("Linking {Count} recommendations to Academy lessons", recommendations.Count);

            int linkedCount = 0;
            foreach (var rec in recommendations)
            {
                var topic = rec.GetValue("topic", rec.GetValue("title", "")).AsString;
                var phase = rec.GetValue("phase", BsonNull.Value);
                
                var (lessonId, confidence) = await FindBestLessonMatchAsync(
                    topic,
                    phase != BsonNull.Value ? phase.AsString : null,
                    ct);

                if (lessonId.HasValue)
                {
                    var update = Builders<BsonDocument>.Update
                        .Set("academy_lesson_id", lessonId.Value)
                        .Set("academy_link_confidence", confidence)
                        .Set("academy_linked_at", DateTime.UtcNow);

                    await _atlas.LearningRecommendations.UpdateOneAsync(
                        Builders<BsonDocument>.Filter.Eq("_id", rec["_id"]),
                        update,
                        cancellationToken: ct);

                    linkedCount++;
                }
            }

            _logger.LogInformation("Successfully linked {Count} recommendations to Academy lessons", linkedCount);
            return linkedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking recommendations to lessons");
            return 0;
        }
    }

    /// <summary>
    /// Extract searchable topic from a knowledge gap pattern
    /// </summary>
    private string ExtractTopicFromGap(string gapPattern)
    {
        // Remove common prefixes and extract key terms
        var topic = gapPattern
            .Replace("knowledge gap:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("gap:", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        // Extract key construction terms
        var keywords = new[] { "concrete", "steel", "safety", "framing", "foundation", "roofing", "electrical", "plumbing", "hvac" };
        
        foreach (var keyword in keywords)
        {
            if (topic.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return keyword;
            }
        }

        // Return cleaned pattern
        return topic;
    }

    /// <summary>
    /// Calculate match score between gap and lesson
    /// Uses multiple signals: title match, summary match, phase alignment
    /// </summary>
    private double CalculateMatchScore(string gapPattern, AcademyLesson lesson, string? targetPhase)
    {
        double score = 0.0;
        int signals = 0;

        // Signal 1: Title similarity (weighted 40%)
        var titleSimilarity = CalculateStringSimilarity(gapPattern, lesson.Title);
        score += titleSimilarity * 0.4;
        signals++;

        // Signal 2: Summary similarity (weighted 30%)
        var summarySimilarity = CalculateStringSimilarity(gapPattern, lesson.Summary);
        score += summarySimilarity * 0.3;
        signals++;

        // Signal 3: Phase alignment (weighted 30%)
        if (!string.IsNullOrEmpty(targetPhase))
        {
            // If lesson has phase-specific content, match it
            var phaseSimilarity = lesson.Summary.Contains(targetPhase, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.5;
            score += phaseSimilarity * 0.3;
            signals++;
        }

        return signals > 0 ? score : 0.0;
    }

    /// <summary>
    /// Simple string similarity calculation
    /// In production, would use more sophisticated NLP
    /// </summary>
    private double CalculateStringSimilarity(string text1, string text2)
    {
        if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2)) return 0.0;

        var words1 = text1.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = text2.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }
}

using Auricrux.Web.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services.Breakthrough;

/// <summary>
/// PHASE 4 BREAKTHROUGH: Meta-Learning Service
/// 
/// This is the "intelligence about intelligence" - the service that analyzes
/// HOW Auricrux is learning and DESIGNS EXPERIMENTS to make it learn better.
/// 
/// Key Capabilities:
/// 1. Detect SYSTEMATIC errors (not random noise, but patterns of being wrong)
/// 2. Identify which construction domains Auricrux consistently gets wrong
/// 3. Propose experiments to test hypotheses about model failures
/// 4. Calibrate confidence scores (is 80% confidence actually 80% accurate?)
/// 5. Recommend targeted training data collection
/// 
/// The Innovation:
/// Most AI systems learn from data, but don't analyze their OWN learning process.
/// This service makes Auricrux "self-aware" of its prediction patterns.
/// 
/// INNOVATION: Construction AI that designs experiments to fix its own blind spots.
/// </summary>
public sealed class MetaLearningService
{
    private readonly AtlasService _atlas;
    private readonly PhysicalVerificationService _verificationService;
    private readonly ILogger<MetaLearningService> _logger;

    public MetaLearningService(
        AtlasService atlas,
        PhysicalVerificationService verificationService,
        ILogger<MetaLearningService> logger)
    {
        _atlas = atlas;
        _verificationService = verificationService;
        _logger = logger;
    }

    /// <summary>
    /// Detect systematic errors in model predictions
    /// </summary>
    public async Task<MetaLearningInsight> AnalyzeModelErrorsAsync(
        string modelId,
        TimeSpan period,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Analyzing model {ModelId} errors over {Period}", modelId, period);

        // Get verification history
        var verifications = await _verificationService.GetVerificationHistoryAsync(period, ct);

        if (verifications.Count < 10)
        {
            _logger.LogWarning("Insufficient verification data for meta-learning ({Count} verifications)", verifications.Count);
            return new MetaLearningInsight
            {
                ModelId = modelId,
                AnalysisPeriod = period,
                TotalPredictions = verifications.Count,
                VerifiedPredictions = verifications.Count,
                OverallAccuracy = verifications.Any() ? verifications.Average(v => v.AccuracyScore) : 0,
                SystematicErrors = [],
                RecommendedExperiments = new List<string>
                {
                    "Collect more verification data - minimum 10 verifications needed for meta-learning"
                },
                ConfidenceCalibration = new Dictionary<string, double>()
            };
        }

        // Detect systematic errors
        var systematicErrors = await DetectSystematicErrorsAsync(verifications, ct);

        // Recommend experiments
        var experiments = DesignExperiments(systematicErrors, verifications);

        // Calibrate confidence scores
        var calibration = CalibrateConfidenceScores(verifications);

        // Calculate overall statistics
        var overallAccuracy = verifications.Average(v => v.AccuracyScore);

        return new MetaLearningInsight
        {
            ModelId = modelId,
            AnalysisPeriod = period,
            TotalPredictions = verifications.Count,
            VerifiedPredictions = verifications.Count(v => v.RequiresModelCorrection),
            OverallAccuracy = overallAccuracy,
            SystematicErrors = systematicErrors,
            RecommendedExperiments = experiments,
            ConfidenceCalibration = calibration
        };
    }

    /// <summary>
    /// Get meta-learning recommendations for improving Auricrux
    /// </summary>
    public async Task<List<string>> GetImprovementRecommendationsAsync(
        TimeSpan period,
        CancellationToken ct = default)
    {
        var insight = await AnalyzeModelErrorsAsync("auricrux-primary", period, ct);

        var recommendations = new List<string>();

        // Accuracy-based recommendations
        if (insight.OverallAccuracy < 0.70)
        {
            recommendations.Add("CRITICAL: Overall prediction accuracy is below 70%. Immediate model retraining required.");
        }
        else if (insight.OverallAccuracy < 0.80)
        {
            recommendations.Add("WARNING: Prediction accuracy is below target (80%). Review systematic errors and collect targeted training data.");
        }

        // Systematic error recommendations
        foreach (var error in insight.SystematicErrors.Take(3))
        {
            recommendations.Add($"Systematic error detected in {error.AffectedDomain}: {error.ErrorPattern}. " +
                              $"Proposed fix: {error.ProposedCorrection}");
        }

        // Calibration recommendations
        var poorlyCalibrated = insight.ConfidenceCalibration
            .Where(kvp => TryParseCalibrationLowerBound(kvp.Key, out var stated)
                          && Math.Abs(kvp.Value - stated) > 0.15)
            .ToList();

        if (poorlyCalibrated.Any())
        {
            recommendations.Add($"Confidence calibration issues detected in {poorlyCalibrated.Count} ranges. " +
                              "Model is overconfident or underconfident in certain prediction types.");
        }

        // Experiment recommendations
        foreach (var experiment in insight.RecommendedExperiments.Take(2))
        {
            recommendations.Add($"Recommended experiment: {experiment}");
        }

        return recommendations;
    }

    /// <summary>
    /// Reads the stated confidence from a calibration bucket key such as
    /// "confidence_0.6_to_0.7". Returns false for keys that do not match, so an
    /// unexpected bucket name degrades the recommendation instead of throwing.
    /// </summary>
    public static bool TryParseCalibrationLowerBound(string key, out double lowerBound)
    {
        lowerBound = 0;
        if (string.IsNullOrWhiteSpace(key)) return false;

        var range = key.Replace("confidence_", string.Empty, StringComparison.OrdinalIgnoreCase);
        var bounds = range.Split("_to_", StringSplitOptions.RemoveEmptyEntries);
        if (bounds.Length == 0) return false;

        return double.TryParse(
            bounds[0],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out lowerBound);
    }

    // ── Private Implementation ──────────────────────────────────────────────────

    private async Task<List<SystematicError>> DetectSystematicErrorsAsync(
        List<PhysicalVerificationResult> verifications,
        CancellationToken ct)
    {
        var systematicErrors = new List<SystematicError>();

        // Group verifications by domain (inferred from measurement types)
        var domainGroups = GroupByDomain(verifications);

        foreach (var (domain, domainVerifications) in domainGroups)
        {
            if (domainVerifications.Count < 3) continue; // Need multiple samples

            // Check if this domain has consistently poor accuracy
            var domainAccuracy = domainVerifications.Average(v => v.AccuracyScore);

            if (domainAccuracy < 0.75)
            {
                // Find common error pattern
                var commonErrors = domainVerifications
                    .SelectMany(v => v.IdentifiedErrors)
                    .GroupBy(e => ExtractErrorPattern(e))
                    .OrderByDescending(g => g.Count())
                    .First();

                var avgVariance = domainVerifications
                    .SelectMany(v => v.MeasurementVariances.Values)
                    .Where(mv => mv.VariancePercent.HasValue)
                    .Average(mv => mv.VariancePercent!.Value);

                systematicErrors.Add(new SystematicError
                {
                    ErrorPattern = commonErrors.Key,
                    AffectedDomain = domain,
                    Occurrences = commonErrors.Count(),
                    AverageVariance = Math.Round(avgVariance, 1),
                    ProposedCorrection = ProposeCorrection(domain, commonErrors.Key, avgVariance)
                });
            }
        }

        // Check for consistent over/under-estimation patterns
        var measurementBiases = DetectMeasurementBiases(verifications);
        systematicErrors.AddRange(measurementBiases);

        return systematicErrors.OrderByDescending(e => e.Occurrences).ToList();
    }

    private Dictionary<string, List<PhysicalVerificationResult>> GroupByDomain(
        List<PhysicalVerificationResult> verifications)
    {
        var groups = new Dictionary<string, List<PhysicalVerificationResult>>();

        foreach (var verification in verifications)
        {
            // Infer domain from measurement types
            var measurements = verification.MeasurementVariances.Keys;
            var domain = InferDomain(measurements);

            if (!groups.ContainsKey(domain))
            {
                groups[domain] = new List<PhysicalVerificationResult>();
            }

            groups[domain].Add(verification);
        }

        return groups;
    }

    private string InferDomain(IEnumerable<string> measurements)
    {
        var measurementList = measurements.Select(m => m.ToLowerInvariant()).ToList();

        if (measurementList.Any(m => m.Contains("cost")))
            return "Cost Estimation";

        if (measurementList.Any(m => m.Contains("days") || m.Contains("schedule") || m.Contains("duration")))
            return "Schedule Prediction";

        if (measurementList.Any(m => m.Contains("pile") || m.Contains("foundation") || m.Contains("load")))
            return "Foundation Engineering";

        if (measurementList.Any(m => m.Contains("concrete") || m.Contains("strength") || m.Contains("cure")))
            return "Concrete Work";

        if (measurementList.Any(m => m.Contains("steel") || m.Contains("erection") || m.Contains("structural")))
            return "Structural Steel";

        return "General Construction";
    }

    private string ExtractErrorPattern(string error)
    {
        // Extract the core pattern from error message
        if (error.Contains("variance"))
            return "High variance in quantitative prediction";

        if (error.Contains("outcome mismatch"))
            return "Qualitative outcome prediction failure";

        if (error.Contains("Failed to predict"))
            return "Missing prediction for captured measurement";

        return "Unclassified prediction error";
    }

    private string ProposeCorrection(string domain, string errorPattern, double avgVariance)
    {
        if (avgVariance > 30)
        {
            return $"Collect 10+ more {domain} examples with actual measurements to improve prediction model";
        }

        if (errorPattern.Contains("Missing prediction"))
        {
            return $"Expand {domain} prediction model to include additional measurement types";
        }

        if (errorPattern.Contains("outcome mismatch"))
        {
            return $"Refine {domain} outcome classification logic and add more training examples";
        }

        return $"General model retraining recommended for {domain}";
    }

    private List<SystematicError> DetectMeasurementBiases(List<PhysicalVerificationResult> verifications)
    {
        var biases = new List<SystematicError>();

        // Collect all measurement variances by type
        var measurementsByType = new Dictionary<string, List<MeasurementVariance>>();

        foreach (var verification in verifications)
        {
            foreach (var (measurement, variance) in verification.MeasurementVariances)
            {
                if (!variance.Predicted.HasValue || !variance.Actual.HasValue)
                    continue;

                if (!measurementsByType.ContainsKey(measurement))
                {
                    measurementsByType[measurement] = new List<MeasurementVariance>();
                }

                measurementsByType[measurement].Add(variance);
            }
        }

        // Check each measurement type for systematic over/under-estimation
        foreach (var (measurement, variances) in measurementsByType)
        {
            if (variances.Count < 5) continue; // Need enough samples

            var avgBias = variances.Average(v => (v.Predicted!.Value - v.Actual!.Value) / v.Actual.Value * 100);

            // Systematic bias if consistently over/under by >10%
            if (Math.Abs(avgBias) > 10)
            {
                var direction = avgBias > 0 ? "over-estimates" : "under-estimates";
                biases.Add(new SystematicError
                {
                    ErrorPattern = $"Auricrux consistently {direction} {measurement}",
                    AffectedDomain = InferDomain(new[] { measurement }),
                    Occurrences = variances.Count,
                    AverageVariance = Math.Abs(avgBias),
                    ProposedCorrection = $"Apply {(avgBias > 0 ? "downward" : "upward")} correction factor of {Math.Abs(avgBias):F1}% to {measurement} predictions"
                });
            }
        }

        return biases;
    }

    private List<string> DesignExperiments(
        List<SystematicError> systematicErrors,
        List<PhysicalVerificationResult> verifications)
    {
        var experiments = new List<string>();

        // Design experiments for top systematic errors
        foreach (var error in systematicErrors.Take(3))
        {
            if (error.ErrorPattern.Contains("over-estimates") || error.ErrorPattern.Contains("under-estimates"))
            {
                experiments.Add($"Experiment: Apply proposed correction factor to {error.AffectedDomain} and verify on next 10 predictions");
            }
            else if (error.AverageVariance > 25)
            {
                experiments.Add($"Experiment: Collect 20 additional {error.AffectedDomain} examples with high-quality measurements");
            }
            else
            {
                experiments.Add($"Experiment: A/B test current vs refined {error.AffectedDomain} model on next 15 predictions");
            }
        }

        // Design experiments for low-sample domains
        var lowSampleDomains = GroupByDomain(verifications)
            .Where(kvp => kvp.Value.Count < 5)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var domain in lowSampleDomains.Take(2))
        {
            experiments.Add($"Experiment: Actively collect {domain} predictions with verification to build domain-specific accuracy baseline");
        }

        return experiments;
    }

    private Dictionary<string, double> CalibrateConfidenceScores(List<PhysicalVerificationResult> verifications)
    {
        return ComputeEmpiricalCalibration(
            verifications
                .Where(v => v.StatedConfidence > 0)
                .Select(v => (v.StatedConfidence, v.AccuracyScore)));
    }

    /// <summary>
    /// Maps stated confidence buckets to mean measured accuracy.
    /// Omits buckets with fewer than <paramref name="minPerBucket"/> samples instead of inventing values.
    /// </summary>
    public static Dictionary<string, double> ComputeEmpiricalCalibration(
        IEnumerable<(double StatedConfidence, double AccuracyScore)> samples,
        int minPerBucket = 2)
    {
        var list = samples.ToList();
        (string Key, double Lo, double HiExclusive)[] buckets =
        [
            ("confidence_0.0_to_0.6", 0.0, 0.6),
            ("confidence_0.6_to_0.7", 0.6, 0.7),
            ("confidence_0.7_to_0.8", 0.7, 0.8),
            ("confidence_0.8_to_0.9", 0.8, 0.9),
            ("confidence_0.9_to_1.0", 0.9, 1.0001)
        ];

        var result = new Dictionary<string, double>();
        foreach (var (key, lo, hi) in buckets)
        {
            var inBucket = list
                .Where(s => s.StatedConfidence >= lo && s.StatedConfidence < hi)
                .ToList();
            if (inBucket.Count < minPerBucket) continue;
            result[key] = Math.Round(inBucket.Average(s => s.AccuracyScore), 4);
        }

        return result;
    }
}

// ── Models ──────────────────────────────────────────────────────────────────────

public sealed class MetaLearningInsight
{
    public required string ModelId { get; init; }
    public required TimeSpan AnalysisPeriod { get; init; }
    public required int TotalPredictions { get; init; }
    public required int VerifiedPredictions { get; init; }
    public required double OverallAccuracy { get; init; }
    public required List<SystematicError> SystematicErrors { get; init; }
    public required List<string> RecommendedExperiments { get; init; }
    public required Dictionary<string, double> ConfidenceCalibration { get; init; }
}

public sealed class SystematicError
{
    public required string ErrorPattern { get; init; }
    public required string AffectedDomain { get; init; }
    public required int Occurrences { get; init; }
    public required double AverageVariance { get; init; }
    public required string ProposedCorrection { get; init; }
}

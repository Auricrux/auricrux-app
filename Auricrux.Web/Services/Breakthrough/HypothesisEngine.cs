using System.Collections.Concurrent;
using Auricrux.Web.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.Services.Breakthrough;

/// <summary>
/// PHASE 4 BREAKTHROUGH: Hypothesis Engine
/// 
/// Generates COMPETING construction hypotheses for decisions, not just one answer.
/// Each hypothesis includes:
/// - Different construction approaches
/// - Quantitative predictions (cost, time, safety metrics)
/// - Explicit assumptions
/// - Risk factors
/// 
/// This enables:
/// 1. Side-by-side comparison of approaches BEFORE execution
/// 2. Explicit capture of what Auricrux predicted
/// 3. Later verification against actual outcomes
/// 4. Detection of which approaches Auricrux consistently gets wrong
/// 
/// INNOVATION: No other construction AI generates falsifiable, competing hypotheses.
/// </summary>
public sealed class HypothesisEngine
{
    private readonly AtlasService _atlas;
    private readonly ILogger<HypothesisEngine> _logger;
    /// <summary>
    /// Process-local cache so verification works when Atlas is not configured
    /// (demo / sovereign / CI paths). Atlas remains the durable store when available.
    /// </summary>
    private static readonly ConcurrentDictionary<string, ConstructionHypothesis> MemoryByHypothesisId = new();
    private static readonly ConcurrentDictionary<string, HypothesisComparison> MemoryByDecisionId = new();

    public HypothesisEngine(AtlasService atlas, ILogger<HypothesisEngine> logger)
    {
        _atlas = atlas;
        _logger = logger;
    }

    /// <summary>
    /// Generate multiple competing hypotheses for a construction decision
    /// </summary>
    public async Task<HypothesisComparison> GenerateHypothesesAsync(
        string decisionContext,
        string constructionPhase,
        string? projectId = null,
        Dictionary<string, object>? knownConstraints = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Generating competing hypotheses for decision in phase: {Phase}", constructionPhase);

        var decisionId = Guid.NewGuid().ToString();
        var hypotheses = await GenerateCompetingApproachesAsync(decisionContext, constructionPhase, knownConstraints, ct);

        var comparison = new HypothesisComparison
        {
            DecisionId = decisionId,
            DecisionContext = decisionContext,
            ConstructionPhase = constructionPhase,
            ProjectId = projectId,
            Hypotheses = hypotheses,
            RecommendedApproach = SelectRecommendedApproach(hypotheses),
            Reasoning = BuildRecommendationReasoning(hypotheses),
            GeneratedAt = DateTime.UtcNow
        };

        CacheInMemory(comparison);

        // Persist to Atlas for later verification
        if (_atlas.IsConfigured)
        {
            await PersistHypothesesAsync(comparison, ct);
        }

        return comparison;
    }

    /// <summary>
    /// Look up a single hypothesis by id (memory first, then Atlas).
    /// Used by PhysicalVerificationService when closing the self-correction loop.
    /// </summary>
    public async Task<ConstructionHypothesis?> FindHypothesisByIdAsync(string hypothesisId, CancellationToken ct = default)
    {
        if (MemoryByHypothesisId.TryGetValue(hypothesisId, out var cached))
            return cached;

        if (!_atlas.IsConfigured) return null;

        try
        {
            var filter = Builders<BsonDocument>.Filter.ElemMatch<BsonValue>(
                "hypotheses",
                new BsonDocument { ["hypothesis_id"] = hypothesisId });

            var doc = await _atlas.Database!
                .GetCollection<BsonDocument>("hypothesis_comparisons")
                .Find(filter)
                .FirstOrDefaultAsync(ct);

            if (doc == null) return null;

            var hypothesisDoc = doc["hypotheses"].AsBsonArray
                .Select(h => h.AsBsonDocument)
                .FirstOrDefault(h => h["hypothesis_id"].AsString == hypothesisId);

            if (hypothesisDoc == null) return null;

            var hypothesis = MapHypothesisFromBson(hypothesisDoc);
            MemoryByHypothesisId[hypothesisId] = hypothesis;
            return hypothesis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving hypothesis {HypothesisId}", hypothesisId);
            return null;
        }
    }

    /// <summary>
    /// Retrieve a hypothesis comparison for verification
    /// </summary>
    public async Task<HypothesisComparison?> GetHypothesesAsync(string decisionId, CancellationToken ct = default)
    {
        if (MemoryByDecisionId.TryGetValue(decisionId, out var cached))
            return cached;

        if (!_atlas.IsConfigured) return null;

        try
        {
            var filter = Builders<BsonDocument>.Filter.Eq("decision_id", decisionId);
            var doc = await _atlas.Database
                .GetCollection<BsonDocument>("hypothesis_comparisons")
                .Find(filter)
                .FirstOrDefaultAsync(ct);

            if (doc == null) return null;

            var mapped = MapFromBson(doc);
            CacheInMemory(mapped);
            return mapped;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving hypotheses for decision {DecisionId}", decisionId);
            return null;
        }
    }

    private static void CacheInMemory(HypothesisComparison comparison)
    {
        MemoryByDecisionId[comparison.DecisionId] = comparison;
        foreach (var h in comparison.Hypotheses)
            MemoryByHypothesisId[h.HypothesisId] = h;
    }

    private static ConstructionHypothesis MapHypothesisFromBson(BsonDocument hypothesisDoc) => new()
    {
        HypothesisId = hypothesisDoc["hypothesis_id"].AsString,
        Approach = hypothesisDoc["approach"].AsString,
        PredictedOutcome = hypothesisDoc["predicted_outcome"].AsString,
        Assumptions = hypothesisDoc["assumptions"].AsBsonArray.Select(a => a.AsString).ToList(),
        QuantitativePredictions = hypothesisDoc["quantitative_predictions"].AsBsonDocument.ToDictionary(
            e => e.Name,
            e => e.Value.ToDouble()),
        ConfidenceScore = hypothesisDoc["confidence_score"].ToDouble(),
        RiskFactors = hypothesisDoc["risk_factors"].AsBsonArray.Select(r => r.AsString).ToList()
    };

    // ── Private Implementation ──────────────────────────────────────────────────

    private async Task<List<ConstructionHypothesis>> GenerateCompetingApproachesAsync(
        string decisionContext,
        string phase,
        Dictionary<string, object>? constraints,
        CancellationToken ct)
    {
        // In production, this would use sophisticated construction knowledge + physics models
        // For now, demonstrate the structure with domain-grounded examples

        var hypotheses = new List<ConstructionHypothesis>();

        // Hypothesis generation is phase-specific
        if (decisionContext.Contains("pour", StringComparison.OrdinalIgnoreCase) ||
            phase.Contains("pour", StringComparison.OrdinalIgnoreCase))
        {
            hypotheses.AddRange(GenerateFoundationPourHypotheses(decisionContext, constraints));
        }
        else if (phase.Contains("foundation", StringComparison.OrdinalIgnoreCase) ||
            phase.Contains("concrete", StringComparison.OrdinalIgnoreCase))
        {
            hypotheses.AddRange(GenerateFoundationHypotheses(decisionContext, constraints));
        }
        else if (phase.Contains("steel", StringComparison.OrdinalIgnoreCase) ||
                 phase.Contains("structural", StringComparison.OrdinalIgnoreCase))
        {
            hypotheses.AddRange(GenerateStructuralHypotheses(decisionContext, constraints));
        }
        else if (phase.Contains("schedule", StringComparison.OrdinalIgnoreCase) ||
                 phase.Contains("delay", StringComparison.OrdinalIgnoreCase))
        {
            hypotheses.AddRange(GenerateScheduleHypotheses(decisionContext, constraints));
        }
        else
        {
            // Generic construction hypotheses
            hypotheses.AddRange(GenerateGenericHypotheses(decisionContext, constraints));
        }

        return hypotheses;
    }

    private List<ConstructionHypothesis> GenerateFoundationPourHypotheses(
        string context,
        Dictionary<string, object>? constraints)
    {
        var targetPsi = 4000.0;
        if (constraints != null && constraints.TryGetValue("target_psi", out var psiObj) &&
            double.TryParse(psiObj?.ToString(), out var parsedPsi))
        {
            targetPsi = parsedPsi;
        }

        return
        [
            new ConstructionHypothesis
            {
                HypothesisId = Guid.NewGuid().ToString(),
                Approach = "Standard 4000 PSI Mix — Day Pour, Ambient Cure",
                PredictedOutcome = "Meets design strength at 28 days with normal finishing window",
                Assumptions =
                [
                    "Ambient temperature 50–70°F during place and cure",
                    "Water-cement ratio held to mix design",
                    "Vibration and finishing per ACI 301",
                    "No extended truck wait beyond 90 minutes"
                ],
                QuantitativePredictions = new Dictionary<string, double>
                {
                    { "compressive_strength_psi_7d", targetPsi * 0.65 },
                    { "compressive_strength_psi_28d", targetPsi },
                    { "slump_inches", 4.0 },
                    { "cure_days_to_stripping", 7 },
                    { "cold_joint_risk_percent", 8 }
                },
                ConfidenceScore = 0.84,
                RiskFactors =
                [
                    "Hot-weather set acceleration if temps rise",
                    "Finishers overworking surface → scaling",
                    "Supply delay creating cold joints"
                ]
            },
            new ConstructionHypothesis
            {
                HypothesisId = Guid.NewGuid().ToString(),
                Approach = "Cold-Weather Pour — Heated Mix + Insulated Blankets",
                PredictedOutcome = "Protected early strength; slightly longer schedule, lower freeze risk",
                Assumptions =
                [
                    "Mix water heated; aggregates above freezing",
                    "Insulated blankets placed within 1 hour of finish",
                    "Cylinder cures match field protection protocol",
                    "ACI 306 cold-weather plan active"
                ],
                QuantitativePredictions = new Dictionary<string, double>
                {
                    { "compressive_strength_psi_7d", targetPsi * 0.55 },
                    { "compressive_strength_psi_28d", targetPsi * 0.98 },
                    { "slump_inches", 3.5 },
                    { "cure_days_to_stripping", 10 },
                    { "cold_joint_risk_percent", 5 }
                },
                ConfidenceScore = 0.79,
                RiskFactors =
                [
                    "Blanket gaps causing surface freeze",
                    "Under-heated mix slowing set",
                    "Cylinder strength not matching field if protection removed early"
                ]
            },
            new ConstructionHypothesis
            {
                HypothesisId = Guid.NewGuid().ToString(),
                Approach = "Accelerated High-Early Mix — Fast Form Cycle",
                PredictedOutcome = "Earlier stripping strength; higher cost and thermal cracking risk",
                Assumptions =
                [
                    "Type III or accelerator dosage within manufacturer limits",
                    "Thermal control plan for mass sections",
                    "QA cylinders cast for 1-day and 3-day breaks",
                    "Finishing crew sized for faster set"
                ],
                QuantitativePredictions = new Dictionary<string, double>
                {
                    { "compressive_strength_psi_7d", targetPsi * 0.85 },
                    { "compressive_strength_psi_28d", targetPsi * 1.05 },
                    { "slump_inches", 5.0 },
                    { "cure_days_to_stripping", 3 },
                    { "cold_joint_risk_percent", 12 }
                },
                ConfidenceScore = 0.71,
                RiskFactors =
                [
                    "Plastic shrinkage if wind/sun high",
                    "Thermal cracking in thicker sections",
                    "Cost overrun from admixture premium"
                ]
            }
        ];
    }

    private List<ConstructionHypothesis> GenerateFoundationHypotheses(
        string context,
        Dictionary<string, object>? constraints)
    {
        return new List<ConstructionHypothesis>
        {
            new ConstructionHypothesis
            {
                HypothesisId = Guid.NewGuid().ToString(),
                Approach = "Standard Driven Pile Foundation",
                PredictedOutcome = "High load capacity with moderate installation time and cost",
                Assumptions = new List<string>
                {
                    "Soil bearing capacity < 2000 psf at shallow depth",
                    "No groundwater issues within 20ft",
                    "Access for pile driving equipment available",
                    "Vibration tolerance acceptable for surroundings"
                },
                QuantitativePredictions = new Dictionary<string, double>
                {
                    { "cost_per_pile_usd", 8500 },
                    { "installation_days", 12 },
                    { "load_capacity_tons", 85 },
                    { "settlement_risk_percent", 5 }
                },
                ConfidenceScore = 0.82,
                RiskFactors = new List<string>
                {
                    "Vibration impact on adjacent structures",
                    "Encountering unexpected bedrock depth",
                    "Weather delays during installation"
                }
            },
            new ConstructionHypothesis
            {
                HypothesisId = Guid.NewGuid().ToString(),
                Approach = "Drilled Shaft (Caisson) Foundation",
                PredictedOutcome = "Highest load capacity with lower vibration but higher cost",
                Assumptions = new List<string>
                {
                    "Soil conditions allow for open-hole drilling",
                    "Dewatering available if needed",
                    "Concrete truck access confirmed",
                    "Rebar cage fabrication on schedule"
                },
                QuantitativePredictions = new Dictionary<string, double>
                {
                    { "cost_per_shaft_usd", 14200 },
                    { "installation_days", 18 },
                    { "load_capacity_tons", 120 },
                    { "settlement_risk_percent", 2 }
                },
                ConfidenceScore = 0.75,
                RiskFactors = new List<string>
                {
                    "Caving during drilling if unstable soil",
                    "Concrete placement quality issues",
                    "Higher cost overrun risk",
                    "Weather impact on open holes"
                }
            },
            new ConstructionHypothesis
            {
                HypothesisId = Guid.NewGuid().ToString(),
                Approach = "Shallow Spread Footing with Ground Improvement",
                PredictedOutcome = "Cost-effective if soil improvement successful, higher schedule risk",
                Assumptions = new List<string>
                {
                    "Soil improvement (compaction/stone columns) achieves target bearing",
                    "Settlement monitoring acceptable",
                    "No differential settlement concerns",
                    "Load distribution uniform"
                },
                QuantitativePredictions = new Dictionary<string, double>
                {
                    { "cost_per_footing_usd", 5800 },
                    { "total_days_with_improvement", 24 },
                    { "load_capacity_tons", 60 },
                    { "settlement_risk_percent", 12 }
                },
                ConfidenceScore = 0.68,
                RiskFactors = new List<string>
                {
                    "Ground improvement may not achieve target density",
                    "Differential settlement between footings",
                    "Requires extensive testing and verification",
                    "Settlement monitoring long-term"
                }
            }
        };
    }

    private List<ConstructionHypothesis> GenerateStructuralHypotheses(
        string context,
        Dictionary<string, object>? constraints)
    {
        return new List<ConstructionHypothesis>
        {
            new ConstructionHypothesis
            {
                HypothesisId = Guid.NewGuid().ToString(),
                Approach = "Conventional Steel Erection Sequence",
                PredictedOutcome = "Proven approach with predictable timeline",
                Assumptions = new List<string>
                {
                    "Steel fabrication on schedule",
                    "Crane access confirmed",
                    "Bolted connections per AISC/RCSC spec",
                    "Weather windows available"
                },
                QuantitativePredictions = new Dictionary<string, double>
                {
                    { "erection_days", 45 },
                    { "crew_size", 8 },
                    { "cost_per_ton_usd", 2400 },
                    { "safety_incidents_risk_percent", 8 }
                },
                ConfidenceScore = 0.88,
                RiskFactors = new List<string>
                {
                    "Weather delays",
                    "Crane breakdown",
                    "Bolt torque QC failures"
                }
            },
            new ConstructionHypothesis
            {
                HypothesisId = Guid.NewGuid().ToString(),
                Approach = "Accelerated Modular Steel Assembly",
                PredictedOutcome = "Faster erection but higher coordination complexity",
                Assumptions = new List<string>
                {
                    "Ground-level pre-assembly space available",
                    "Larger crane capacity for module lifts",
                    "Field welding crew coordinated",
                    "Module fit-up tolerance within 1/8\""
                },
                QuantitativePredictions = new Dictionary<string, double>
                {
                    { "erection_days", 28 },
                    { "crew_size", 12 },
                    { "cost_per_ton_usd", 2750 },
                    { "safety_incidents_risk_percent", 5 }
                },
                ConfidenceScore = 0.72,
                RiskFactors = new List<string>
                {
                    "Module fit-up issues",
                    "Field welding delays",
                    "Higher upfront coordination cost"
                }
            }
        };
    }

    private List<ConstructionHypothesis> GenerateScheduleHypotheses(
        string context,
        Dictionary<string, object>? constraints)
    {
        return new List<ConstructionHypothesis>
        {
            new ConstructionHypothesis
            {
                HypothesisId = Guid.NewGuid().ToString(),
                Approach = "Sequential Recovery with Float Reallocation",
                PredictedOutcome = "Methodical recovery, minimal additional cost",
                Assumptions = new List<string>
                {
                    "Float exists on critical path successor activities",
                    "No further delays occur",
                    "Resource leveling maintains productivity"
                },
                QuantitativePredictions = new Dictionary<string, double>
                {
                    { "recovery_days", 14 },
                    { "additional_cost_usd", 8500 },
                    { "success_probability_percent", 75 }
                },
                ConfidenceScore = 0.79,
                RiskFactors = new List<string>
                {
                    "Float may already be consumed",
                    "Subsequent delays compound recovery"
                }
            },
            new ConstructionHypothesis
            {
                HypothesisId = Guid.NewGuid().ToString(),
                Approach = "Crash Critical Path Activities",
                PredictedOutcome = "Fastest recovery but highest cost and risk",
                Assumptions = new List<string>
                {
                    "Overtime and double shifts available",
                    "Trade labor available",
                    "Quality does not degrade with acceleration"
                },
                QuantitativePredictions = new Dictionary<string, double>
                {
                    { "recovery_days", 7 },
                    { "additional_cost_usd", 42000 },
                    { "success_probability_percent", 60 }
                },
                ConfidenceScore = 0.65,
                RiskFactors = new List<string>
                {
                    "Crew fatigue and safety incidents",
                    "Quality issues from rushing",
                    "High cost may not be recoverable"
                }
            }
        };
    }

    private List<ConstructionHypothesis> GenerateGenericHypotheses(
        string context,
        Dictionary<string, object>? constraints)
    {
        return new List<ConstructionHypothesis>
        {
            new ConstructionHypothesis
            {
                HypothesisId = Guid.NewGuid().ToString(),
                Approach = "Standard Approach - By-the-Book Execution",
                PredictedOutcome = "Predictable outcome following industry standards",
                Assumptions = new List<string>
                {
                    "Plans and specifications are complete",
                    "Standard trade practices apply",
                    "No unforeseen site conditions"
                },
                QuantitativePredictions = new Dictionary<string, double>
                {
                    { "baseline_cost_multiplier", 1.0 },
                    { "baseline_schedule_multiplier", 1.0 },
                    { "risk_level_percent", 15 }
                },
                ConfidenceScore = 0.80,
                RiskFactors = new List<string>
                {
                    "Assumptions may not hold in field",
                    "Coordination gaps"
                }
            },
            new ConstructionHypothesis
            {
                HypothesisId = Guid.NewGuid().ToString(),
                Approach = "Accelerated Alternative Approach",
                PredictedOutcome = "Faster execution with trade-offs",
                Assumptions = new List<string>
                {
                    "Alternative methods are code-compliant",
                    "Resources available for acceleration",
                    "Quality can be maintained"
                },
                QuantitativePredictions = new Dictionary<string, double>
                {
                    { "baseline_cost_multiplier", 1.15 },
                    { "baseline_schedule_multiplier", 0.75 },
                    { "risk_level_percent", 25 }
                },
                ConfidenceScore = 0.68,
                RiskFactors = new List<string>
                {
                    "Higher cost",
                    "Increased coordination complexity"
                }
            }
        };
    }

    private string SelectRecommendedApproach(List<ConstructionHypothesis> hypotheses)
    {
        // Select based on confidence score and predicted outcome quality
        var best = hypotheses.OrderByDescending(h => h.ConfidenceScore).First();
        return best.Approach;
    }

    private string BuildRecommendationReasoning(List<ConstructionHypothesis> hypotheses)
    {
        var recommended = hypotheses.OrderByDescending(h => h.ConfidenceScore).First();
        return $"Recommended '{recommended.Approach}' based on {recommended.ConfidenceScore:P0} confidence. " +
               $"This approach balances {string.Join(", ", recommended.Assumptions.Take(2))}. " +
               $"Key risks: {string.Join("; ", recommended.RiskFactors.Take(2))}.";
    }

    private async Task PersistHypothesesAsync(HypothesisComparison comparison, CancellationToken ct)
    {
        try
        {
            var doc = new BsonDocument
            {
                ["decision_id"] = comparison.DecisionId,
                ["decision_context"] = comparison.DecisionContext,
                ["construction_phase"] = comparison.ConstructionPhase,
                ["project_id"] = comparison.ProjectId ?? "",
                ["hypotheses"] = new BsonArray(comparison.Hypotheses.Select(h => new BsonDocument
                {
                    ["hypothesis_id"] = h.HypothesisId,
                    ["approach"] = h.Approach,
                    ["predicted_outcome"] = h.PredictedOutcome,
                    ["assumptions"] = new BsonArray(h.Assumptions),
                    ["quantitative_predictions"] = new BsonDocument(h.QuantitativePredictions.Select(kvp =>
                        new BsonElement(kvp.Key, kvp.Value))),
                    ["confidence_score"] = h.ConfidenceScore,
                    ["risk_factors"] = new BsonArray(h.RiskFactors)
                })),
                ["recommended_approach"] = comparison.RecommendedApproach,
                ["reasoning"] = comparison.Reasoning,
                ["generated_at"] = comparison.GeneratedAt,
                ["verified"] = false
            };

            await _atlas.Database
                .GetCollection<BsonDocument>("hypothesis_comparisons")
                .InsertOneAsync(doc, cancellationToken: ct);

            _logger.LogInformation("Persisted hypothesis comparison {DecisionId} with {Count} hypotheses",
                comparison.DecisionId, comparison.Hypotheses.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist hypothesis comparison");
        }
    }

    private HypothesisComparison MapFromBson(BsonDocument doc)
    {
        var hypotheses = doc["hypotheses"].AsBsonArray
            .Select(h => new ConstructionHypothesis
            {
                HypothesisId = h["hypothesis_id"].AsString,
                Approach = h["approach"].AsString,
                PredictedOutcome = h["predicted_outcome"].AsString,
                Assumptions = h["assumptions"].AsBsonArray.Select(a => a.AsString).ToList(),
                QuantitativePredictions = h["quantitative_predictions"].AsBsonDocument.ToDictionary(
                    e => e.Name,
                    e => e.Value.ToDouble()),
                ConfidenceScore = h["confidence_score"].ToDouble(),
                RiskFactors = h["risk_factors"].AsBsonArray.Select(r => r.AsString).ToList()
            })
            .ToList();

        return new HypothesisComparison
        {
            DecisionId = doc["decision_id"].AsString,
            DecisionContext = doc["decision_context"].AsString,
            ConstructionPhase = doc["construction_phase"].AsString,
            ProjectId = doc.GetValue("project_id", "").AsString,
            Hypotheses = hypotheses,
            RecommendedApproach = doc["recommended_approach"].AsString,
            Reasoning = doc["reasoning"].AsString,
            GeneratedAt = doc["generated_at"].ToUniversalTime()
        };
    }
}

// ── Models ──────────────────────────────────────────────────────────────────────

public sealed class HypothesisComparison
{
    public required string DecisionId { get; init; }
    public required string DecisionContext { get; init; }
    public required string ConstructionPhase { get; init; }
    public string? ProjectId { get; init; }
    public required List<ConstructionHypothesis> Hypotheses { get; init; }
    public required string RecommendedApproach { get; init; }
    public required string Reasoning { get; init; }
    public DateTime GeneratedAt { get; init; }
}

public sealed class ConstructionHypothesis
{
    public required string HypothesisId { get; init; }
    public required string Approach { get; init; }
    public required string PredictedOutcome { get; init; }
    public required List<string> Assumptions { get; init; }
    public required Dictionary<string, double> QuantitativePredictions { get; init; }
    public required double ConfidenceScore { get; init; }
    public required List<string> RiskFactors { get; init; }
}

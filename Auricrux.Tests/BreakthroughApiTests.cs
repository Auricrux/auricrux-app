using System.Net;
using System.Net.Http.Json;
using Auricrux.Web.Services.Breakthrough;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Auricrux.Tests;

/// <summary>
/// Exercises the breakthrough API surface end to end with no Atlas configured:
/// hypotheses → verification → accuracy → meta-learning → provable reasoning.
/// </summary>
public sealed class BreakthroughApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public BreakthroughApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(_ => { }).CreateClient();
    }

    [Fact]
    public async Task Hypotheses_endpoint_returns_competing_approaches_with_predictions()
    {
        var response = await _client.PostAsJsonAsync("/api/breakthrough/hypotheses", new
        {
            decisionContext = "Foundation pour for 8-inch slab, 4000 PSI, overnight low near freezing",
            constructionPhase = "foundation-pour",
            projectId = $"api-test-{Guid.NewGuid():N}",
            constraints = new Dictionary<string, double>
            {
                ["target_psi"] = 4000,
                ["ambient_temp_f"] = 38,
                ["slab_thickness_in"] = 8
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comparison = await response.Content.ReadFromJsonAsync<HypothesisComparison>();

        Assert.NotNull(comparison);
        Assert.True(comparison!.Hypotheses.Count >= 3);
        Assert.False(string.IsNullOrWhiteSpace(comparison.RecommendedApproach));
        Assert.All(comparison.Hypotheses, h => Assert.NotEmpty(h.QuantitativePredictions));
    }

    [Fact]
    public async Task Hypotheses_endpoint_rejects_missing_context()
    {
        var response = await _client.PostAsJsonAsync("/api/breakthrough/hypotheses", new
        {
            decisionContext = "",
            constructionPhase = "foundation-pour"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generated_hypotheses_are_retrievable_and_verifiable_without_atlas()
    {
        var generate = await _client.PostAsJsonAsync("/api/breakthrough/hypotheses", new
        {
            decisionContext = "Cold weather foundation pour with tight truck spacing",
            constructionPhase = "foundation-pour",
            constraints = new Dictionary<string, double> { ["target_psi"] = 4000, ["ambient_temp_f"] = 40 }
        });
        var comparison = await generate.Content.ReadFromJsonAsync<HypothesisComparison>();
        Assert.NotNull(comparison);

        var fetched = await _client.GetAsync($"/api/breakthrough/hypotheses/{comparison!.DecisionId}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);

        var chosen = comparison.Hypotheses[0];
        var underPerforming = chosen.QuantitativePredictions
            .ToDictionary(kv => kv.Key, kv => kv.Value * 0.6);

        var verify = await _client.PostAsJsonAsync("/api/breakthrough/verify", new
        {
            predictionId = chosen.HypothesisId,
            actualOutcome = "Cylinders broke low at 7 days; stripping delayed",
            actualMeasurements = underPerforming,
            verifiedBy = "api-test"
        });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var result = await verify.Content.ReadFromJsonAsync<PhysicalVerificationResult>();

        Assert.NotNull(result);
        Assert.True(result!.RequiresModelCorrection);
        Assert.NotEmpty(result.MeasurementVariances);
        Assert.True(result.AccuracyScore < 1.0);
    }

    [Fact]
    public async Task Verify_endpoint_rejects_empty_measurements()
    {
        var response = await _client.PostAsJsonAsync("/api/breakthrough/verify", new
        {
            predictionId = "does-not-matter",
            actualMeasurements = new Dictionary<string, double>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_decision_returns_not_found()
    {
        var response = await _client.GetAsync($"/api/breakthrough/hypotheses/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Provable_reasoning_returns_steps_and_standards()
    {
        var response = await _client.PostAsJsonAsync("/api/breakthrough/provable-reasoning", new
        {
            question = "Will a 4000 PSI foundation pour reach stripping strength in 7 days at 45F?",
            physicalParameters = new Dictionary<string, double>
            {
                ["target_psi"] = 4000,
                ["ambient_temp_f"] = 45,
                ["required_strip_psi"] = 2800
            },
            applicableCodes = new[] { "ACI 318", "ACI 301", "ACI 306" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var proof = await response.Content.ReadFromJsonAsync<ProvableReasoningResult>();

        Assert.NotNull(proof);
        Assert.False(string.IsNullOrWhiteSpace(proof!.Conclusion));
        Assert.NotEmpty(proof.ProofSteps);
        Assert.NotEmpty(proof.CitedStandards);
    }

    [Fact]
    public async Task Provable_reasoning_rejects_missing_question()
    {
        var response = await _client.PostAsJsonAsync("/api/breakthrough/provable-reasoning", new { question = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Accuracy_and_meta_learning_endpoints_respond_without_atlas()
    {
        var accuracy = await _client.GetAsync("/api/breakthrough/accuracy?periodHours=168");
        Assert.Equal(HttpStatusCode.OK, accuracy.StatusCode);

        var meta = await _client.GetAsync("/api/breakthrough/meta-learning/auricrux-primary?periodHours=168");
        Assert.Equal(HttpStatusCode.OK, meta.StatusCode);
        var insight = await meta.Content.ReadFromJsonAsync<MetaLearningInsight>();
        Assert.NotNull(insight);

        var recommendations = await _client.GetAsync("/api/breakthrough/improvement-recommendations");
        Assert.Equal(HttpStatusCode.OK, recommendations.StatusCode);
    }

    [Fact]
    public async Task Dashboard_reports_in_memory_breakthrough_instead_of_bare_zeros()
    {
        // Guarantee at least one verification exists in the in-process cache.
        await _client.PostAsync("/api/breakthrough/demo/foundation-pour", content: null);

        var response = await _client.GetAsync("/api/intelligence/dashboard/breakthrough?period=7d");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var activity = await response.Content.ReadFromJsonAsync<BreakthroughActivityPayload>();
        Assert.NotNull(activity);
        Assert.Equal("in-memory", activity!.Persistence);
        Assert.True(activity.VerificationsRecorded > 0);

        var overview = await _client.GetAsync("/api/intelligence/dashboard/overview?period=7d");
        Assert.Equal(HttpStatusCode.OK, overview.StatusCode);
        var payload = await overview.Content.ReadFromJsonAsync<OverviewPayload>();
        Assert.NotNull(payload);
        Assert.Equal("in_memory_breakthrough", payload!.Status);
        Assert.False(string.IsNullOrWhiteSpace(payload.StatusMessage));
    }

    private sealed class BreakthroughActivityPayload
    {
        public string Persistence { get; set; } = "";
        public int VerificationsRecorded { get; set; }
        public double AverageAccuracy { get; set; }
    }

    private sealed class OverviewPayload
    {
        public string Status { get; set; } = "";
        public string StatusMessage { get; set; } = "";
        public int OutcomesVerified { get; set; }
    }
}

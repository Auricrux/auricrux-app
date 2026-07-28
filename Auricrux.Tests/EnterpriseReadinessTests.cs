using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Auricrux.Tests;

/// <summary>
/// Enterprise readiness probes (AUX-013): correlation IDs, structured API errors, capabilities matrix.
/// </summary>
public sealed class EnterpriseReadinessTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EnterpriseReadinessTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(_ => { }).CreateClient();
    }

    [Fact]
    public async Task Api_responses_include_correlation_id_header()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        Assert.False(string.IsNullOrWhiteSpace(response.Headers.GetValues("X-Correlation-Id").First()));
    }

    [Fact]
    public async Task Client_supplied_correlation_id_is_echoed()
    {
        var expected = $"test-{Guid.NewGuid():N}";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("X-Correlation-Id", expected);

        var response = await _client.SendAsync(request);
        Assert.Equal(expected, response.Headers.GetValues("X-Correlation-Id").First());
    }

    [Fact]
    public async Task Capabilities_endpoint_reports_shipped_features_and_honest_gaps()
    {
        var response = await _client.GetAsync("/api/capabilities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CapabilitiesPayload>();
        Assert.NotNull(payload);
        Assert.Equal("Auricrux", payload!.App);
        Assert.True(payload.CorpusEntries >= 75, "Corpus should have 75+ entries after expansion.");
        Assert.Contains("ChatGPT", payload.Competitors);
        Assert.Contains(payload.Features, f => f.Name.Contains("Multi-model chat") && f.Status == "shipped");
        Assert.Contains(payload.Features, f => f.Name.Contains("Fine-tuned") && f.Status == "blocked");
        Assert.False(payload.ConstructionMoat.PromotedFineTuneLive);
        Assert.Contains("PARTIAL", payload.ParityScore.OverallAssessment, StringComparison.OrdinalIgnoreCase);
        Assert.True(payload.CompetitiveMatrix.Count >= 15, "Per-competitor matrix should cover major feature rows.");
        Assert.Contains(payload.CompetitiveMatrix, r =>
            r.Feature.Contains("Construction specialist corpus") && r.Auricrux == "shipped");
        Assert.True(payload.CorpusStats.TotalEntries >= 75);
        Assert.True(payload.CorpusStats.Categories.Count >= 5, "Corpus should span multiple domain categories.");
        Assert.True(payload.ParityScore.MatrixRows >= 15);
    }

    [Fact]
    public async Task Capabilities_matrix_honestly_marks_peer_gaps()
    {
        var response = await _client.GetAsync("/api/capabilities");
        var payload = await response.Content.ReadFromJsonAsync<CapabilitiesPayload>();
        Assert.NotNull(payload);

        var agentic = payload!.CompetitiveMatrix.First(r => r.Feature.Contains("Agentic plugins"));
        Assert.Equal("planned", agentic.Auricrux);
        Assert.Equal("yes", agentic.Peers["ChatGPT"]);

        var fineTune = payload.CompetitiveMatrix.First(r => r.Feature.Contains("fine-tuned weights"));
        Assert.Equal("blocked", fineTune.Auricrux);
        Assert.Equal("no", fineTune.Peers["Claude"]);
    }

    [Fact]
    public async Task Health_reports_expanded_corpus_depth()
    {
        var response = await _client.GetAsync("/api/health");
        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>();
        Assert.NotNull(payload);
        Assert.True(payload!.CorpusEntries >= 75);
    }

    private sealed class HealthPayload
    {
        public int CorpusEntries { get; set; }
    }

    private sealed class CapabilitiesPayload
    {
        public string App { get; set; } = string.Empty;
        public int CorpusEntries { get; set; }
        public List<string> Competitors { get; set; } = [];
        public List<FeaturePayload> Features { get; set; } = [];
        public List<MatrixRowPayload> CompetitiveMatrix { get; set; } = [];
        public CorpusStatsPayload CorpusStats { get; set; } = new();
        public MoatPayload ConstructionMoat { get; set; } = new();
        public ParityPayload ParityScore { get; set; } = new();
    }

    private sealed class MatrixRowPayload
    {
        public string Feature { get; set; } = string.Empty;
        public string Auricrux { get; set; } = string.Empty;
        public Dictionary<string, string> Peers { get; set; } = [];
    }

    private sealed class CorpusStatsPayload
    {
        public int TotalEntries { get; set; }
        public Dictionary<string, int> Categories { get; set; } = [];
    }

    private sealed class FeaturePayload
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private sealed class MoatPayload
    {
        public bool PromotedFineTuneLive { get; set; }
    }

    private sealed class ParityPayload
    {
        public string OverallAssessment { get; set; } = string.Empty;
        public int MatrixRows { get; set; }
    }
}

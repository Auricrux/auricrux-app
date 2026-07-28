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
        Assert.True(payload.CorpusEntries >= 55, "Corpus should have 55+ entries after expansion.");
        Assert.Contains("ChatGPT", payload.Competitors);
        Assert.Contains(payload.Features, f => f.Name.Contains("Multi-model chat") && f.Status == "shipped");
        Assert.Contains(payload.Features, f => f.Name.Contains("Fine-tuned") && f.Status == "blocked");
        Assert.False(payload.ConstructionMoat.PromotedFineTuneLive);
        Assert.Contains("PARTIAL", payload.ParityScore.OverallAssessment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_reports_expanded_corpus_depth()
    {
        var response = await _client.GetAsync("/api/health");
        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>();
        Assert.NotNull(payload);
        Assert.True(payload!.CorpusEntries >= 55);
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
        public MoatPayload ConstructionMoat { get; set; } = new();
        public ParityPayload ParityScore { get; set; } = new();
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
    }
}

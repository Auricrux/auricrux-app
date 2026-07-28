using System.Net;
using System.Net.Http.Json;
using Auricrux.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Auricrux.Tests;

public sealed class AuricruxApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuricruxApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(_ => { }).CreateClient();
    }

    [Fact]
    public async Task Health_reports_corpus_and_runtime_mode()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.True(response.IsSuccessStatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Status));
        Assert.True(payload.CorpusEntries > 0);
        Assert.Contains("sqlite", payload.MemoryBackends, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_returns_corpus_hits_not_static_placeholders()
    {
        var response = await _client.PostAsJsonAsync("/api/search", new SearchRequest
        {
            Query = "concrete formwork",
            Scope = SearchScope.Both
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.True(payload.TotalResults > 0);
        Assert.Contains("concrete", payload.Results[0].Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Result 1", payload.Results[0].Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Thinking_modes_return_non_empty_results()
    {
        foreach (var mode in new[] { ThinkingMode.Quick, ThinkingMode.Auto, ThinkingMode.Deep })
        {
            var response = await _client.PostAsJsonAsync("/api/thinking", new ThinkingRequest
            {
                Query = "Sequence a slab pour",
                Mode = mode
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<ThinkingResponse>();
            Assert.NotNull(payload);
            Assert.True(payload!.Success);
            Assert.False(string.IsNullOrWhiteSpace(payload.Result));
        }
    }

    [Fact]
    public async Task Security_headers_are_applied()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
    }

    [Fact]
    public async Task Freemium_blocks_pro_model_on_free_plan()
    {
        const string email = "freemium-gate@test.local";
        var register = await _client.PostAsJsonAsync("/api/account/register", new { email });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using var chatRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat?model=mistral")
        {
            Content = JsonContent.Create(new ChatRequest { Query = "What is a sill plate?" })
        };
        chatRequest.Headers.TryAddWithoutValidation("X-Auricrux-Email", email);

        var chat = await _client.SendAsync(chatRequest);
        Assert.Equal(HttpStatusCode.Forbidden, chat.StatusCode);
    }

    [Fact]
    public async Task Freemium_allows_default_model_after_register()
    {
        const string email = "freemium-ok@test.local";
        var register = await _client.PostAsJsonAsync("/api/account/register", new { email });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using var chatRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat?model=llama3.2")
        {
            Content = JsonContent.Create(new ChatRequest { Query = "What is a sill plate?" })
        };
        chatRequest.Headers.TryAddWithoutValidation("X-Auricrux-Email", email);

        var chat = await _client.SendAsync(chatRequest);
        Assert.Equal(HttpStatusCode.OK, chat.StatusCode);
        var payload = await chat.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Content));
    }

    private sealed class HealthPayload
    {
        public string Status { get; set; } = string.Empty;
        public int CorpusEntries { get; set; }
        public List<string> MemoryBackends { get; set; } = [];
        public string RuntimeMode { get; set; } = string.Empty;
    }
}

using System.Net;
using System.Net.Http.Json;
using Auricrux.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Auricrux.Tests;

/// <summary>
/// Production-facing integration tests for Auricrux.Web. Runs against the real
/// ConstructionIntelligenceService, FreemiumAccountStore (SQLite), ConversationMemoryService
/// (session/JSONL/SQLite), WorkspaceStorageService, and MediaGenerationService — not mocks.
/// Ollama is optional: when unreachable, the service gracefully falls back to its
/// corpus-grounded deterministic response path, and these tests still assert real behavior
/// (non-empty, non-placeholder content) either way.
/// </summary>
public sealed class AuricruxApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuricruxApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(_ => { }).CreateClient();
    }

    // ---------- Health ----------

    [Fact]
    public async Task Health_reports_corpus_and_runtime_mode()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.True(response.IsSuccessStatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Status));
        Assert.True(payload.CorpusEntries >= 55, "Construction corpus should have real depth (55+ entries).");
        Assert.False(string.IsNullOrWhiteSpace(payload.RuntimeMode));
    }

    [Fact]
    public async Task Health_reports_all_three_memory_backends()
    {
        var response = await _client.GetAsync("/api/health");
        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>();

        Assert.NotNull(payload);
        Assert.Contains("session", payload!.MemoryBackends, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("file-jsonl", payload.MemoryBackends, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("sqlite", payload.MemoryBackends, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_via_healthz_alias_also_succeeds()
    {
        var response = await _client.GetAsync("/healthz");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Models_endpoint_lists_configured_models()
    {
        var response = await _client.GetAsync("/api/models");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ModelsPayload>();
        Assert.NotNull(payload);
        Assert.True(payload!.Models.Count >= 3);
    }

    // ---------- Search ----------

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
    public async Task Search_internal_scope_only_returns_internal_entries()
    {
        var response = await _client.PostAsJsonAsync("/api/search", new SearchRequest
        {
            Query = "osha fall protection scaffold",
            Scope = SearchScope.Internal
        });

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(payload);
        // OSHA content in this corpus is tagged "public" scope; internal-only search should not surface it.
        Assert.DoesNotContain(payload!.Results, r => r.Title.Contains("OSHA", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_public_scope_only_returns_public_entries()
    {
        var response = await _client.PostAsJsonAsync("/api/search", new SearchRequest
        {
            Query = "concrete slab formwork csi",
            Scope = SearchScope.Public
        });

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(payload);
        // CSI concrete content is tagged "internal" scope; public-only search should not surface it.
        Assert.DoesNotContain(payload!.Results, r => r.Title.Contains("CSI Division 03", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_requires_query()
    {
        var response = await _client.PostAsJsonAsync("/api/search", new SearchRequest { Query = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Thinking ----------

    [Theory]
    [InlineData(ThinkingMode.Quick)]
    [InlineData(ThinkingMode.Auto)]
    [InlineData(ThinkingMode.Deep)]
    public async Task Thinking_modes_return_non_empty_results(ThinkingMode mode)
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
        Assert.Equal(mode, payload.Mode);
        Assert.False(string.IsNullOrWhiteSpace(payload.Result));
    }

    [Fact]
    public async Task Thinking_requires_query()
    {
        var response = await _client.PostAsJsonAsync("/api/thinking", new ThinkingRequest { Query = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Chat + feedback ----------

    [Fact]
    public async Task Chat_returns_response_with_sources_and_confidence()
    {
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Query = "What is a sill plate?",
            SearchScope = SearchScope.Both
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Content));
        Assert.True(payload.ConfidenceScore > 0);
        Assert.NotNull(payload.InteractionId);
    }

    [Fact]
    public async Task Chat_requires_query()
    {
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest { Query = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Feedback_records_rating_for_existing_interaction()
    {
        var chatResponse = await _client.PostAsJsonAsync("/api/chat", new ChatRequest { Query = "Rough estimate for a 20x30 garage slab" });
        var chatPayload = await chatResponse.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(chatPayload?.InteractionId);

        var feedbackResponse = await _client.PostAsJsonAsync(
            $"/api/feedback/{chatPayload!.InteractionId}",
            new StarRating { Stars = 5, Comment = "Helpful" });

        Assert.Equal(HttpStatusCode.Accepted, feedbackResponse.StatusCode);
    }

    [Fact]
    public async Task Feedback_returns_not_found_for_unknown_interaction()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/feedback/{Guid.NewGuid()}",
            new StarRating { Stars = 3 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Feedback_rejects_invalid_star_rating()
    {
        var chatResponse = await _client.PostAsJsonAsync("/api/chat", new ChatRequest { Query = "OSHA fall protection basics" });
        var chatPayload = await chatResponse.Content.ReadFromJsonAsync<ChatResponse>();

        var feedbackResponse = await _client.PostAsJsonAsync(
            $"/api/feedback/{chatPayload!.InteractionId}",
            new StarRating { Stars = 9 });

        Assert.Equal(HttpStatusCode.BadRequest, feedbackResponse.StatusCode);
    }

    // ---------- Security / platform controls ----------

    [Fact]
    public async Task Security_headers_are_applied()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
    }

    [Fact]
    public async Task Cors_preflight_allows_configured_origin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/health");
        request.Headers.Add("Origin", "http://localhost:5080");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Secure_endpoint_open_when_auth_disabled_by_default()
    {
        // Default test host runs with Auth:Enabled=false — anonymous dev mode.
        var response = await _client.GetAsync("/api/secure/ping");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- Freemium (AUX-003) ----------

    [Fact]
    public async Task Freemium_register_creates_free_plan_defaults()
    {
        var email = $"freemium-defaults-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync("/api/account/register", new { email });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var account = await response.Content.ReadFromJsonAsync<AccountPayload>();
        Assert.NotNull(account);
        Assert.Equal("free", account!.Plan);
        Assert.Equal(25, account.DailyQueryLimit);
        Assert.Equal(0, account.QueriesUsedToday);
    }

    [Fact]
    public async Task Freemium_blocks_pro_model_on_free_plan()
    {
        var email = $"freemium-gate-{Guid.NewGuid():N}@test.local";
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
        var email = $"freemium-ok-{Guid.NewGuid():N}@test.local";
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

    [Fact]
    public async Task Freemium_daily_limit_returns_402_once_exceeded()
    {
        var email = $"freemium-limit-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/account/register", new { email });
        var account = await register.Content.ReadFromJsonAsync<AccountPayload>();
        Assert.NotNull(account);

        HttpResponseMessage? last = null;
        for (var i = 0; i < account!.DailyQueryLimit; i++)
        {
            last = await _client.PostAsync($"/api/account/{email}/consume", content: null);
            Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        }

        var overLimit = await _client.PostAsync($"/api/account/{email}/consume", content: null);
        Assert.Equal(HttpStatusCode.PaymentRequired, overLimit.StatusCode);
    }

    [Fact]
    public async Task Freemium_upgrade_to_pro_unlocks_higher_limit_and_models()
    {
        var email = $"freemium-upgrade-{Guid.NewGuid():N}@test.local";
        await _client.PostAsJsonAsync("/api/account/register", new { email });

        using var beforeUpgrade = new HttpRequestMessage(HttpMethod.Post, "/api/chat?model=mistral")
        {
            Content = JsonContent.Create(new ChatRequest { Query = "What is a sill plate?" })
        };
        beforeUpgrade.Headers.TryAddWithoutValidation("X-Auricrux-Email", email);
        var forbidden = await _client.SendAsync(beforeUpgrade);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var upgrade = await _client.PostAsJsonAsync($"/api/account/{email}/upgrade", new { plan = "pro" });
        Assert.Equal(HttpStatusCode.OK, upgrade.StatusCode);
        var upgraded = await upgrade.Content.ReadFromJsonAsync<AccountPayload>();
        Assert.NotNull(upgraded);
        Assert.Equal("pro", upgraded!.Plan);
        Assert.Equal(500, upgraded.DailyQueryLimit);

        using var afterUpgrade = new HttpRequestMessage(HttpMethod.Post, "/api/chat?model=mistral")
        {
            Content = JsonContent.Create(new ChatRequest { Query = "What is a sill plate?" })
        };
        afterUpgrade.Headers.TryAddWithoutValidation("X-Auricrux-Email", email);
        var allowed = await _client.SendAsync(afterUpgrade);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Account_plans_endpoint_lists_three_tiers()
    {
        var response = await _client.GetAsync("/api/account/plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("free", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pro-plus", body, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Memory (session / JSONL / SQLite) ----------

    [Fact]
    public async Task Memory_backends_endpoint_lists_three_options()
    {
        var response = await _client.GetAsync("/api/memory/backends");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("session", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file-jsonl", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sqlite", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("file-jsonl")]
    [InlineData("sqlite")]
    public async Task Memory_backend_round_trips_appended_turns(string backend)
    {
        var sessionId = $"session-{backend}-{Guid.NewGuid():N}";
        var append = await _client.PostAsJsonAsync($"/api/memory/{sessionId}", new
        {
            role = "user",
            content = "What is a sill plate?",
            backend
        });
        Assert.Equal(HttpStatusCode.Accepted, append.StatusCode);

        var list = await _client.GetAsync($"/api/memory/{sessionId}?backend={backend}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains("sill plate", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Memory_export_returns_markdown_transcript()
    {
        var sessionId = $"export-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync($"/api/memory/{sessionId}", new
        {
            role = "user",
            content = "Explain retainage on a pay app.",
            backend = "sqlite"
        });
        await _client.PostAsJsonAsync($"/api/memory/{sessionId}", new
        {
            role = "assistant",
            content = "Retainage is typically 5-10% held until substantial completion.",
            backend = "sqlite"
        });

        var export = await _client.GetAsync($"/api/memory/{sessionId}/export?backend=sqlite&format=markdown");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("text/markdown", export.Content.Headers.ContentType?.MediaType);
        var body = await export.Content.ReadAsStringAsync();
        Assert.Contains("# Auricrux conversation", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("retainage", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("## User", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("## Assistant", body, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Workspace files ----------

    [Fact]
    public async Task Workspace_create_folder_upload_download_and_delete_round_trip()
    {
        var folder = $"jobs-{Guid.NewGuid():N}";
        var folderResponse = await _client.PostAsJsonAsync("/api/workspace/folders", new { path = folder });
        Assert.Equal(HttpStatusCode.OK, folderResponse.StatusCode);

        using var form = new MultipartFormDataContent();
        var fileBytes = System.Text.Encoding.UTF8.GetBytes("RFI-001: verify sill plate anchor bolt spacing.");
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", "rfi-001.txt");
        form.Add(new StringContent(folder), "folder");

        var upload = await _client.PostAsync("/api/workspace/files", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);

        var listResponse = await _client.GetAsync($"/api/workspace?path={folder}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.Contains("rfi-001.txt", listBody, StringComparison.OrdinalIgnoreCase);

        var download = await _client.GetAsync($"/api/workspace/files/{folder}/rfi-001.txt");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        var downloaded = await download.Content.ReadAsStringAsync();
        Assert.Contains("sill plate", downloaded, StringComparison.OrdinalIgnoreCase);

        var delete = await _client.DeleteAsync($"/api/workspace/{folder}/rfi-001.txt");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    // ---------- Media generation (offline-first) ----------

    [Fact]
    public async Task Media_image_generation_uses_offline_renderer_without_stable_diffusion()
    {
        // Test host has no Auricrux:StableDiffusionUrl configured, so this must exercise
        // the real offline SVG construction renderer fallback, not a mock.
        var response = await _client.PostAsJsonAsync("/api/media/image", new { prompt = "residential foundation plan isometric" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("offline-svg", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Media_video_generation_produces_storyboard_package()
    {
        var response = await _client.PostAsJsonAsync("/api/media/video", new { prompt = "roof tear-off sequence", frames = 4 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("storyboard", body, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Web static assets (STT/TTS client wiring) ----------

    [Fact]
    public async Task Speech_recognition_script_is_served_with_expected_hooks()
    {
        var response = await _client.GetAsync("/js/auricrux-speech.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var script = await response.Content.ReadAsStringAsync();
        Assert.Contains("auricruxSpeech", script);
        Assert.Contains("start", script);
        Assert.Contains("speak", script);
    }

    private sealed class HealthPayload
    {
        public string Status { get; set; } = string.Empty;
        public int CorpusEntries { get; set; }
        public List<string> MemoryBackends { get; set; } = [];
        public string RuntimeMode { get; set; } = string.Empty;
    }

    private sealed class ModelsPayload
    {
        public List<string> Models { get; set; } = [];
    }

    private sealed class AccountPayload
    {
        public string Email { get; set; } = string.Empty;
        public string Plan { get; set; } = string.Empty;
        public int DailyQueryLimit { get; set; }
        public int QueriesUsedToday { get; set; }
    }
}

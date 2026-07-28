using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Auricrux.Tests;

/// <summary>
/// AUX-021: proves the OIDC/JWT authentication pipeline is default-deny when explicitly
/// enabled. Auth:Enabled defaults to false in appsettings.json (anonymous dev mode); this
/// factory flips it on with a syntactically valid but unreachable Authority so the JwtBearer
/// handler is registered and enforced without requiring any live identity provider or network
/// access (a missing/invalid bearer token never needs to contact the Authority to be rejected).
/// </summary>
public sealed class AuthEnabledWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Enabled"] = "true",
                ["Auth:Authority"] = "https://auricrux-eval-issuer.invalid/",
                ["Auth:Audience"] = "auricrux"
            });
        });
    }
}

public sealed class AuthEnabledTests : IClassFixture<AuthEnabledWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthEnabledTests(AuthEnabledWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Secure_endpoint_denies_by_default_when_auth_enabled_without_token()
    {
        var response = await _client.GetAsync("/api/secure/ping");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Secure_endpoint_denies_malformed_bearer_token()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/secure/ping");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer not-a-real-jwt");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Public_health_endpoint_still_works_when_auth_enabled()
    {
        // Health is intentionally not [Authorize]-gated so uptime probes keep working.
        var response = await _client.GetAsync("/api/health");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Auth_status_endpoint_reports_enabled_without_oidc_client_configured()
    {
        var response = await _client.GetAsync("/account/auth-status");
        Assert.True(response.IsSuccessStatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"authEnabled\":true", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"oidcConfigured\":false", body, StringComparison.OrdinalIgnoreCase);
    }
}

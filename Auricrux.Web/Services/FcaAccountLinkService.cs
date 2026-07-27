using System.Collections.Concurrent;
using System.Net.Http.Json;

namespace Auricrux.Web.Services;

/// <summary>
/// Optional FCA Ecosystem account attachment for Auricrux App users who already have FCA entitlements.
/// </summary>
public sealed class FcaAccountLinkService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ConcurrentDictionary<string, FcaLinkRecord> _links = new(StringComparer.OrdinalIgnoreCase);

    public FcaAccountLinkService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<FcaLinkRecord> LinkAsync(string auricruxEmail, string fcaBearerToken, CancellationToken ct = default)
    {
        var baseUrl = (_config["Auricrux:FcaEcosystemApiBase"] ?? "http://localhost:5000").TrimEnd('/');
        var client = _httpClientFactory.CreateClient(nameof(FcaAccountLinkService));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/entitlements/me");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {fcaBearerToken.Trim()}");
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"FCA account probe failed ({(int)response.StatusCode}). Check token and FCA API base.");
        }

        var snapshot = await response.Content.ReadFromJsonAsync<FcaEntitlementSnapshot>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty FCA entitlements response.");

        var record = new FcaLinkRecord(
            AuricruxEmail: auricruxEmail.Trim().ToLowerInvariant(),
            LinkedAtUtc: DateTime.UtcNow,
            Plan: snapshot.Plan,
            HasAcademy: snapshot.HasAcademy,
            HasCte: snapshot.HasCte,
            HasEmbeddedAuricrux: snapshot.HasEmbeddedAuricrux,
            Status: snapshot.Status);
        _links[record.AuricruxEmail] = record;
        return record;
    }

    public FcaLinkRecord? Get(string auricruxEmail)
        => _links.TryGetValue(auricruxEmail.Trim().ToLowerInvariant(), out var link) ? link : null;

    public bool Unlink(string auricruxEmail)
        => _links.TryRemove(auricruxEmail.Trim().ToLowerInvariant(), out _);
}

public sealed record FcaEntitlementSnapshot(
    string Plan,
    string Status,
    bool HasAcademy,
    bool HasCte,
    bool HasEmbeddedAuricrux);

public sealed record FcaLinkRecord(
    string AuricruxEmail,
    DateTime LinkedAtUtc,
    string Plan,
    bool HasAcademy,
    bool HasCte,
    bool HasEmbeddedAuricrux,
    string Status);

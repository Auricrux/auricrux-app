using System.Collections.Concurrent;
using Auricrux.Web.Controllers;

namespace Auricrux.Web.Services;

/// <summary>
/// In-process freemium account store. Production would swap for durable storage.
/// </summary>
public sealed class FreemiumAccountStore
{
    private readonly ConcurrentDictionary<string, AuricruxAccount> _accounts = new(StringComparer.OrdinalIgnoreCase);

    public AuricruxAccount Register(string email)
    {
        var key = Normalize(email);
        return _accounts.GetOrAdd(key, _ => new AuricruxAccount
        {
            Email = key,
            Plan = "free",
            DailyQueryLimit = 25,
            QueriesUsedToday = 0,
            DayKey = DateOnly.FromDateTime(DateTime.UtcNow)
        });
    }

    public bool TryGet(string email, out AuricruxAccount? account)
    {
        var found = _accounts.TryGetValue(Normalize(email), out var value);
        account = value;
        if (found && account is not null)
        {
            account.RolloverDay();
        }

        return found;
    }

    public bool Upgrade(string email, string plan)
    {
        if (!_accounts.TryGetValue(Normalize(email), out var account))
        {
            return false;
        }

        account.Plan = plan.Trim().ToLowerInvariant() switch
        {
            "pro" => "pro",
            "pro-plus" => "pro-plus",
            _ => "free"
        };
        account.DailyQueryLimit = account.Plan switch
        {
            "pro" => 500,
            "pro-plus" => 5000,
            _ => 25
        };
        return true;
    }

    public (bool ok, bool limitReached) TryConsume(string email)
    {
        if (!_accounts.TryGetValue(Normalize(email), out var account))
        {
            return (false, false);
        }

        account.RolloverDay();
        if (account.QueriesUsedToday >= account.DailyQueryLimit)
        {
            return (true, true);
        }

        account.QueriesUsedToday += 1;
        return (true, false);
    }

    public IReadOnlyList<string> AllowedModels(AuricruxAccount account) => account.Plan switch
    {
        "pro" or "pro-plus" => ["llama3.2", "mistral", "auricrux"],
        _ => ["llama3.2"]
    };

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

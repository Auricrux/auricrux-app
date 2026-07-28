using Auricrux.Web.Services;
using Xunit;

namespace Auricrux.Tests;

/// <summary>
/// AUX-003: proves the freemium account store is durable (SQLite-backed), not just an
/// in-process dictionary that resets on restart. Two independent store instances pointed
/// at the same database file simulate a process restart between requests.
/// </summary>
public sealed class FreemiumAccountStoreDurabilityTests : IDisposable
{
    private readonly string _dbPath;

    public FreemiumAccountStoreDurabilityTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"auricrux-accounts-test-{Guid.NewGuid():N}.db");
    }

    [Fact]
    public void Account_upgrade_persists_across_simulated_process_restart()
    {
        const string email = "durable-upgrade@test.local";

        var firstProcess = new FreemiumAccountStore(_dbPath);
        firstProcess.Register(email);
        var upgraded = firstProcess.Upgrade(email, "pro");
        Assert.True(upgraded);

        // Simulate a fresh process by constructing a brand-new store instance against the
        // same SQLite file — no shared in-memory state between these two objects.
        var secondProcess = new FreemiumAccountStore(_dbPath);
        var found = secondProcess.TryGet(email, out var account);

        Assert.True(found);
        Assert.NotNull(account);
        Assert.Equal("pro", account!.Plan);
        Assert.Equal(500, account.DailyQueryLimit);
    }

    [Fact]
    public void Consumed_quota_persists_across_simulated_process_restart()
    {
        const string email = "durable-quota@test.local";

        var firstProcess = new FreemiumAccountStore(_dbPath);
        firstProcess.Register(email);
        for (var i = 0; i < 3; i++)
        {
            firstProcess.TryConsume(email);
        }

        var secondProcess = new FreemiumAccountStore(_dbPath);
        secondProcess.TryGet(email, out var account);

        Assert.NotNull(account);
        Assert.Equal(3, account!.QueriesUsedToday);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}

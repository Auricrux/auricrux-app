using System.Collections.Concurrent;
using Auricrux.Web.Controllers;
using Microsoft.Data.Sqlite;

namespace Auricrux.Web.Services;

/// <summary>
/// Durable freemium account store backed by SQLite so plan upgrades and daily usage
/// survive an app restart (accounts.db under Data/accounts). An in-process cache keeps
/// hot reads fast; every mutation is written through to SQLite immediately.
/// </summary>
public sealed class FreemiumAccountStore
{
    private readonly string _dbPath;
    private readonly ConcurrentDictionary<string, AuricruxAccount> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _writeGate = new();

    public FreemiumAccountStore(IWebHostEnvironment env)
        : this(Path.Combine(env.ContentRootPath, "Data", "accounts", "accounts.db"))
    {
    }

    /// <summary>
    /// Testable constructor that points directly at a SQLite file path. Production DI uses
    /// the <see cref="IWebHostEnvironment"/> overload; tests can pass a temp path to prove
    /// durability across separate store instances (simulated process restarts).
    /// </summary>
    public FreemiumAccountStore(string databasePath)
    {
        _dbPath = databasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        EnsureSchema();
        LoadCache();
    }

    public AuricruxAccount Register(string email)
    {
        var key = Normalize(email);
        var existing = _cache.GetOrAdd(key, _ =>
        {
            var loaded = LoadFromDb(key);
            if (loaded is not null)
            {
                return loaded;
            }

            var created = new AuricruxAccount
            {
                Email = key,
                Plan = "free",
                DailyQueryLimit = 25,
                QueriesUsedToday = 0,
                DayKey = DateOnly.FromDateTime(DateTime.UtcNow)
            };
            Persist(created);
            return created;
        });

        return existing;
    }

    public bool TryGet(string email, out AuricruxAccount? account)
    {
        var key = Normalize(email);
        if (!_cache.TryGetValue(key, out var value))
        {
            value = LoadFromDb(key);
            if (value is not null)
            {
                _cache[key] = value;
            }
        }

        account = value;
        if (account is not null && RolloverIfNeeded(account))
        {
            Persist(account);
        }

        return account is not null;
    }

    public bool Upgrade(string email, string plan)
    {
        if (!TryGet(email, out var account) || account is null)
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
        Persist(account);
        return true;
    }

    public (bool ok, bool limitReached) TryConsume(string email)
    {
        if (!TryGet(email, out var account) || account is null)
        {
            return (false, false);
        }

        if (account.QueriesUsedToday >= account.DailyQueryLimit)
        {
            return (true, true);
        }

        account.QueriesUsedToday += 1;
        Persist(account);
        return (true, false);
    }

    public IReadOnlyList<string> AllowedModels(AuricruxAccount account) => account.Plan switch
    {
        "pro" or "pro-plus" => ["llama3.2", "mistral", "auricrux", "auricrux-fca"],
        _ => ["llama3.2"]
    };

    private static bool RolloverIfNeeded(AuricruxAccount account)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (account.DayKey == today)
        {
            return false;
        }

        account.DayKey = today;
        account.QueriesUsedToday = 0;
        return true;
    }

    private void Persist(AuricruxAccount account)
    {
        lock (_writeGate)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO accounts(email, plan, daily_limit, queries_used, day_key)
                VALUES ($email, $plan, $limit, $used, $day)
                ON CONFLICT(email) DO UPDATE SET
                    plan = excluded.plan,
                    daily_limit = excluded.daily_limit,
                    queries_used = excluded.queries_used,
                    day_key = excluded.day_key
                """;
            cmd.Parameters.AddWithValue("$email", account.Email);
            cmd.Parameters.AddWithValue("$plan", account.Plan);
            cmd.Parameters.AddWithValue("$limit", account.DailyQueryLimit);
            cmd.Parameters.AddWithValue("$used", account.QueriesUsedToday);
            cmd.Parameters.AddWithValue("$day", account.DayKey.ToString("O"));
            cmd.ExecuteNonQuery();
        }
    }

    private AuricruxAccount? LoadFromDb(string email)
    {
        lock (_writeGate)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT email, plan, daily_limit, queries_used, day_key FROM accounts WHERE email = $email";
            cmd.Parameters.AddWithValue("$email", email);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new AuricruxAccount
            {
                Email = reader.GetString(0),
                Plan = reader.GetString(1),
                DailyQueryLimit = reader.GetInt32(2),
                QueriesUsedToday = reader.GetInt32(3),
                DayKey = DateOnly.Parse(reader.GetString(4))
            };
        }
    }

    private void LoadCache()
    {
        lock (_writeGate)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT email, plan, daily_limit, queries_used, day_key FROM accounts";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var account = new AuricruxAccount
                {
                    Email = reader.GetString(0),
                    Plan = reader.GetString(1),
                    DailyQueryLimit = reader.GetInt32(2),
                    QueriesUsedToday = reader.GetInt32(3),
                    DayKey = DateOnly.Parse(reader.GetString(4))
                };
                _cache[account.Email] = account;
            }
        }
    }

    private void EnsureSchema()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS accounts (
              email TEXT PRIMARY KEY,
              plan TEXT NOT NULL,
              daily_limit INTEGER NOT NULL,
              queries_used INTEGER NOT NULL,
              day_key TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

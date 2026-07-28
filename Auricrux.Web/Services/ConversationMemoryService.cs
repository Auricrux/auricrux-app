using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Auricrux.Web.Services;

public enum MemoryBackend
{
    Session = 0,
    FileJsonl = 1,
    Sqlite = 2
}

/// <summary>
/// Multiple memory persistence options: session (RAM), JSONL file, and SQLite.
/// </summary>
public sealed class ConversationMemoryService
{
    private readonly IWebHostEnvironment _env;
    private readonly ConcurrentDictionary<string, List<MemoryTurn>> _session = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _fileGate = new();
    private readonly string _sqlitePath;
    private readonly string _jsonlPath;

    public ConversationMemoryService(IWebHostEnvironment env)
    {
        _env = env;
        var data = Path.Combine(env.ContentRootPath, "Data", "memory");
        Directory.CreateDirectory(data);
        _sqlitePath = Path.Combine(data, "conversations.db");
        _jsonlPath = Path.Combine(data, "conversations.jsonl");
        EnsureSqlite();
    }

    public IReadOnlyList<string> Backends => ["session", "file-jsonl", "sqlite"];

    public async Task AppendAsync(string sessionId, MemoryBackend backend, string role, string content, CancellationToken ct = default)
    {
        var turn = new MemoryTurn(sessionId, role, content, DateTime.UtcNow);
        switch (backend)
        {
            case MemoryBackend.Session:
                _session.AddOrUpdate(sessionId,
                    _ => [turn],
                    (_, list) => { list.Add(turn); return list; });
                break;
            case MemoryBackend.FileJsonl:
                lock (_fileGate)
                {
                    File.AppendAllText(_jsonlPath, JsonSerializer.Serialize(turn) + Environment.NewLine);
                }
                break;
            case MemoryBackend.Sqlite:
                await using (var conn = new SqliteConnection($"Data Source={_sqlitePath}"))
                {
                    await conn.OpenAsync(ct);
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO turns(session_id, role, content, created_utc) VALUES ($s, $r, $c, $t)";
                    cmd.Parameters.AddWithValue("$s", sessionId);
                    cmd.Parameters.AddWithValue("$r", role);
                    cmd.Parameters.AddWithValue("$c", content);
                    cmd.Parameters.AddWithValue("$t", turn.CreatedUtc.ToString("O"));
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                break;
        }
    }

    public async Task<IReadOnlyList<MemoryTurn>> ListAsync(string sessionId, MemoryBackend backend, int take = 50, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 500);
        switch (backend)
        {
            case MemoryBackend.Session:
                return _session.TryGetValue(sessionId, out var list)
                    ? list.TakeLast(take).ToList()
                    : [];
            case MemoryBackend.FileJsonl:
                if (!File.Exists(_jsonlPath)) return [];
                lock (_fileGate)
                {
                    return File.ReadLines(_jsonlPath)
                        .Select(line => JsonSerializer.Deserialize<MemoryTurn>(line))
                        .Where(t => t is not null && string.Equals(t.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
                        .Cast<MemoryTurn>()
                        .TakeLast(take)
                        .ToList();
                }
            case MemoryBackend.Sqlite:
                var results = new List<MemoryTurn>();
                await using (var conn = new SqliteConnection($"Data Source={_sqlitePath}"))
                {
                    await conn.OpenAsync(ct);
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = """
                        SELECT session_id, role, content, created_utc
                        FROM turns
                        WHERE session_id = $s
                        ORDER BY id DESC
                        LIMIT $n
                        """;
                    cmd.Parameters.AddWithValue("$s", sessionId);
                    cmd.Parameters.AddWithValue("$n", take);
                    await using var reader = await cmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        results.Add(new MemoryTurn(
                            reader.GetString(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind)));
                    }
                }

                results.Reverse();
                return results;
            default:
                return [];
        }
    }

    public string ToMarkdown(string sessionId, IReadOnlyList<MemoryTurn> turns)
    {
        var lines = new List<string>
        {
            $"# Auricrux conversation — `{sessionId}`",
            "",
            $"Exported: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ} UTC",
            ""
        };

        if (turns.Count == 0)
        {
            lines.Add("_No turns recorded for this session._");
            return string.Join(Environment.NewLine, lines);
        }

        foreach (var turn in turns)
        {
            var heading = turn.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                ? "Assistant"
                : turn.Role.Equals("user", StringComparison.OrdinalIgnoreCase)
                    ? "User"
                    : turn.Role;
            lines.Add($"## {heading} ({turn.CreatedUtc:yyyy-MM-dd HH:mm:ss} UTC)");
            lines.Add("");
            lines.Add(turn.Content.Trim());
            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void EnsureSqlite()
    {
        using var conn = new SqliteConnection($"Data Source={_sqlitePath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS turns (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              session_id TEXT NOT NULL,
              role TEXT NOT NULL,
              content TEXT NOT NULL,
              created_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_turns_session ON turns(session_id);
            """;
        cmd.ExecuteNonQuery();
    }
}

public sealed record MemoryTurn(string SessionId, string Role, string Content, DateTime CreatedUtc);

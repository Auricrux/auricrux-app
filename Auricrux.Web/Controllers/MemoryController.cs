using Auricrux.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

[ApiController]
[Route("api/memory")]
public sealed class MemoryController(ConversationMemoryService memory) : ControllerBase
{
    [HttpGet("backends")]
    public ActionResult<object> Backends() => Ok(new { backends = memory.Backends });

    [HttpGet("{sessionId}")]
    public async Task<ActionResult<object>> List(
        string sessionId,
        [FromQuery] string backend = "sqlite",
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseBackend(backend, out var parsed))
        {
            return BadRequest(new { error = "backend must be session | file-jsonl | sqlite" });
        }

        var turns = await memory.ListAsync(sessionId, parsed, take, cancellationToken);
        return Ok(new { sessionId, backend = backend.ToLowerInvariant(), turns });
    }

    [HttpGet("{sessionId}/export")]
    public async Task<IActionResult> Export(
        string sessionId,
        [FromQuery] string backend = "sqlite",
        [FromQuery] string format = "markdown",
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseBackend(backend, out var parsed))
        {
            return BadRequest(new { error = "backend must be session | file-jsonl | sqlite" });
        }

        var normalizedFormat = format.Trim().ToLowerInvariant();
        if (normalizedFormat is not ("markdown" or "md" or "json"))
        {
            return BadRequest(new { error = "format must be markdown | json" });
        }

        var turns = await memory.ListAsync(sessionId, parsed, take, cancellationToken);
        if (normalizedFormat == "json")
        {
            return Ok(new { sessionId, backend = backend.ToLowerInvariant(), turns });
        }

        var markdown = memory.ToMarkdown(sessionId, turns);
        return Content(markdown, "text/markdown; charset=utf-8");
    }

    [HttpPost("{sessionId}")]
    public async Task<IActionResult> Append(
        string sessionId,
        [FromBody] MemoryAppendRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Role) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Role and content are required." });
        }

        if (!TryParseBackend(request.Backend ?? "sqlite", out var parsed))
        {
            return BadRequest(new { error = "backend must be session | file-jsonl | sqlite" });
        }

        await memory.AppendAsync(sessionId, parsed, request.Role.Trim(), request.Content.Trim(), cancellationToken);
        return Accepted();
    }

    private static bool TryParseBackend(string value, out MemoryBackend backend)
    {
        backend = MemoryBackend.Sqlite;
        switch (value.Trim().ToLowerInvariant())
        {
            case "session":
                backend = MemoryBackend.Session;
                return true;
            case "file":
            case "file-jsonl":
            case "jsonl":
                backend = MemoryBackend.FileJsonl;
                return true;
            case "sqlite":
            case "db":
                backend = MemoryBackend.Sqlite;
                return true;
            default:
                return false;
        }
    }
}

public sealed class MemoryAppendRequest
{
    public string? Role { get; set; }
    public string? Content { get; set; }
    public string? Backend { get; set; }
}

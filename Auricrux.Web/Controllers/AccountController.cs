using System.Collections.Concurrent;
using Auricrux.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

/// <summary>
/// Freemium + paid entitlements for standalone Auricrux App.
/// </summary>
[ApiController]
[Route("api/account")]
public sealed class AccountController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, AuricruxAccount> Accounts = new(StringComparer.OrdinalIgnoreCase);

    [HttpGet("plans")]
    public ActionResult<object> Plans() => Ok(new
    {
        plans = new[]
        {
            new { id = "free", name = "Freemium", price = 0m, cadence = "month", dailyQueries = 25, models = new[] { "llama3.2" } },
            new { id = "pro", name = "Pro", price = 29m, cadence = "month", dailyQueries = 500, models = new[] { "llama3.2", "mistral", "auricrux" } },
            new { id = "pro-plus", name = "Pro Plus", price = 79m, cadence = "month", dailyQueries = 5000, models = new[] { "llama3.2", "mistral", "auricrux" } }
        }
    });

    [HttpPost("register")]
    public ActionResult<AuricruxAccount> Register([FromBody] RegisterRequest request)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { error = "Email is required." });
        }

        var account = Accounts.GetOrAdd(email, _ => new AuricruxAccount
        {
            Email = email,
            Plan = "free",
            DailyQueryLimit = 25,
            QueriesUsedToday = 0,
            DayKey = DateOnly.FromDateTime(DateTime.UtcNow)
        });

        return Ok(account);
    }

    [HttpGet("{email}")]
    public ActionResult<AuricruxAccount> Get(string email)
    {
        if (!Accounts.TryGetValue(email.Trim().ToLowerInvariant(), out var account))
        {
            return NotFound();
        }

        account.RolloverDay();
        return Ok(account);
    }

    [HttpPost("{email}/upgrade")]
    public ActionResult<AuricruxAccount> Upgrade(string email, [FromBody] UpgradeRequest request)
    {
        var key = email.Trim().ToLowerInvariant();
        if (!Accounts.TryGetValue(key, out var account))
        {
            return NotFound();
        }

        account.Plan = request.Plan?.Trim().ToLowerInvariant() switch
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
        return Ok(account);
    }

    [HttpPost("{email}/consume")]
    public ActionResult<object> Consume(string email)
    {
        var key = email.Trim().ToLowerInvariant();
        if (!Accounts.TryGetValue(key, out var account))
        {
            return NotFound();
        }

        account.RolloverDay();
        if (account.QueriesUsedToday >= account.DailyQueryLimit)
        {
            return StatusCode(StatusCodes.Status402PaymentRequired, new
            {
                error = "Freemium daily limit reached. Upgrade to Pro.",
                plan = account.Plan,
                limit = account.DailyQueryLimit
            });
        }

        account.QueriesUsedToday += 1;
        return Ok(account);
    }
}

public sealed class RegisterRequest
{
    public string? Email { get; set; }
}

public sealed class UpgradeRequest
{
    public string? Plan { get; set; }
}

public sealed class AuricruxAccount
{
    public string Email { get; set; } = string.Empty;
    public string Plan { get; set; } = "free";
    public int DailyQueryLimit { get; set; } = 25;
    public int QueriesUsedToday { get; set; }
    public DateOnly DayKey { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public void RolloverDay()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (DayKey != today)
        {
            DayKey = today;
            QueriesUsedToday = 0;
        }
    }
}

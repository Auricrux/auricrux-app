using Auricrux.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

/// <summary>
/// Freemium + paid entitlements for standalone Auricrux App.
/// </summary>
[ApiController]
[Route("api/account")]
public sealed class AccountController(FreemiumAccountStore accounts) : ControllerBase
{
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

        return Ok(accounts.Register(email));
    }

    [HttpGet("{email}")]
    public ActionResult<AuricruxAccount> Get(string email)
    {
        if (!accounts.TryGet(email, out var account) || account is null)
        {
            return NotFound();
        }

        return Ok(account);
    }

    [HttpPost("{email}/upgrade")]
    public ActionResult<AuricruxAccount> Upgrade(string email, [FromBody] UpgradeRequest request)
    {
        if (!accounts.TryGet(email, out var account) || account is null)
        {
            return NotFound();
        }

        if (!accounts.Upgrade(email, request.Plan ?? "free"))
        {
            return NotFound();
        }

        accounts.TryGet(email, out account);
        return Ok(account);
    }

    [HttpPost("{email}/consume")]
    public ActionResult<object> Consume(string email)
    {
        if (!accounts.TryGet(email, out var account) || account is null)
        {
            return NotFound();
        }

        var (ok, limitReached) = accounts.TryConsume(email);
        if (!ok)
        {
            return NotFound();
        }

        if (limitReached)
        {
            return StatusCode(StatusCodes.Status402PaymentRequired, new
            {
                error = "Freemium daily limit reached. Upgrade to Pro.",
                plan = account.Plan,
                limit = account.DailyQueryLimit
            });
        }

        accounts.TryGet(email, out account);
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

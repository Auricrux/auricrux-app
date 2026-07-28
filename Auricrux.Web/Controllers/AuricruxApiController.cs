using Auricrux.Shared.Models;
using Auricrux.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

[ApiController]
[Route("api")]
public sealed class AuricruxApiController(
    ConstructionIntelligenceService intelligence,
    BackendHealthService health,
    CapabilitiesService capabilities,
    FreemiumAccountStore accounts,
    ILogger<AuricruxApiController> logger) : ControllerBase
{
    [HttpGet("health")]
    [HttpGet("/health")]
    [HttpGet("/healthz")]
    public async Task<ActionResult<object>> GetHealth(CancellationToken cancellationToken)
    {
        var report = await health.ProbeAsync(cancellationToken);
        var statusCode = report.Status switch
        {
            "healthy" => StatusCodes.Status200OK,
            "degraded" => StatusCodes.Status200OK,
            _ => StatusCodes.Status503ServiceUnavailable
        };

        return StatusCode(statusCode, report);
    }

    [HttpGet("models")]
    public ActionResult<object> ListModels() => Ok(new { models = intelligence.AvailableModels });

    [HttpGet("capabilities")]
    public ActionResult<CapabilitiesReport> GetCapabilities() => Ok(capabilities.GetReport());

    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> Chat(
        [FromBody] ChatRequest request,
        [FromQuery] string? model,
        [FromHeader(Name = "X-Auricrux-Email")] string? accountEmail,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query is required." });
        }

        model = ResolveModel(model, accountEmail, out var entitlementError);
        if (entitlementError is not null)
        {
            return entitlementError;
        }

        logger.LogInformation("Chat mode={Mode} scope={Scope} model={Model}", request.ThinkingMode, request.SearchScope, model);
        var response = await intelligence.ChatAsync(request, model, cancellationToken);
        return Ok(response);
    }

    [HttpPost("thinking")]
    public async Task<ActionResult<ThinkingResponse>> PostThinking(
        [FromBody] ThinkingRequest request,
        [FromQuery] string? model,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query is required." });
        }

        var response = await intelligence.ThinkAsync(request, model, cancellationToken);
        return Ok(response);
    }

    [HttpPost("search")]
    public ActionResult<SearchResponse> PostSearch([FromBody] SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query is required." });
        }

        return Ok(intelligence.Search(request));
    }

    [HttpPost("feedback/{interactionId:guid}")]
    public IActionResult Feedback(Guid interactionId, [FromBody] StarRating rating)
    {
        if (!intelligence.TryGetInteraction(interactionId, out _))
        {
            return NotFound();
        }

        if (rating.Stars is < 1 or > 5)
        {
            return BadRequest(new { error = "Stars must be 1-5." });
        }

        intelligence.RecordFeedback(interactionId, rating);
        return Accepted();
    }

    private string? ResolveModel(string? model, string? accountEmail, out ActionResult? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(accountEmail))
        {
            return model;
        }

        if (!accounts.TryGet(accountEmail, out var account) || account is null)
        {
            error = NotFound(new { error = "Account not registered." });
            return null;
        }

        var allowed = accounts.AllowedModels(account);
        var selected = string.IsNullOrWhiteSpace(model) ? allowed[0] : model.Trim();
        if (!allowed.Any(m => m.Equals(selected, StringComparison.OrdinalIgnoreCase)))
        {
            error = StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Model not included in current plan. Upgrade to Pro.",
                plan = account.Plan,
                allowed
            });
            return null;
        }

        var (ok, limitReached) = accounts.TryConsume(accountEmail);
        if (!ok)
        {
            error = NotFound(new { error = "Account not registered." });
            return null;
        }

        if (limitReached)
        {
            error = StatusCode(StatusCodes.Status402PaymentRequired, new
            {
                error = "Freemium daily limit reached. Upgrade to Pro.",
                plan = account.Plan,
                limit = account.DailyQueryLimit
            });
            return null;
        }

        return selected;
    }
}

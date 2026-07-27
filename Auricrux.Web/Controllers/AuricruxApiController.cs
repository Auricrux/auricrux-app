using Auricrux.Shared.Models;
using Auricrux.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

[ApiController]
[Route("api")]
public sealed class AuricruxApiController(
    ConstructionIntelligenceService intelligence,
    ILogger<AuricruxApiController> logger) : ControllerBase
{
    [HttpGet("health")]
    [HttpGet("/health")]
    [HttpGet("/healthz")]
    public ActionResult<object> GetHealth()
    {
        return Ok(new
        {
            status = "healthy",
            app = "Auricrux",
            version = "1.0.0",
            models = intelligence.AvailableModels,
            timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("models")]
    public ActionResult<object> ListModels() => Ok(new { models = intelligence.AvailableModels });

    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> Chat(
        [FromBody] ChatRequest request,
        [FromQuery] string? model,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query is required." });
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
}

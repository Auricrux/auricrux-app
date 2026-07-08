using Microsoft.AspNetCore.Mvc;
using Auricrux.Shared.Models;

namespace Auricrux.Web.Controllers;

/// <summary>
/// API controller for Auricrux thinking modes and search
/// </summary>
[ApiController]
[Route("api")]
public class AuricruxApiController : ControllerBase
{
    private readonly ILogger<AuricruxApiController> _logger;

    public AuricruxApiController(ILogger<AuricruxApiController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Health check for API
    /// </summary>
    [HttpGet("health")]
    public ActionResult<object> GetHealth()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Process a thinking mode request
    /// </summary>
    [HttpPost("thinking")]
    public ActionResult<ThinkingResponse> PostThinking([FromBody] ThinkingRequest request)
    {
        _logger.LogInformation("Thinking request received: Mode={Mode}, Query={Query}", request.Mode, request.Query);

        var response = new ThinkingResponse
        {
            Success = true,
            Mode = request.Mode,
            Result = $"Thinking response for query: {request.Query}",
            ProcessingTimeMs = Random.Shared.Next(500, 3000),
            Timestamp = DateTime.UtcNow
        };

        return Ok(response);
    }

    /// <summary>
    /// Process a search request
    /// </summary>
    [HttpPost("search")]
    public ActionResult<SearchResponse> PostSearch([FromBody] SearchRequest request)
    {
        _logger.LogInformation("Search request received: Scope={Scope}, Query={Query}", request.Scope, request.Query);

        var response = new SearchResponse
        {
            Success = true,
            Scope = request.Scope,
            Results = new List<SearchResult>
            {
                new() { Title = "Result 1", Snippet = "Sample search result for query: " + request.Query, Score = 0.95 },
                new() { Title = "Result 2", Snippet = "Another relevant result", Score = 0.87 },
                new() { Title = "Result 3", Snippet = "Additional search result", Score = 0.76 }
            },
            TotalResults = 3,
            Timestamp = DateTime.UtcNow
        };

        return Ok(response);
    }
}

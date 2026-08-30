using Auricrux.Web.Services.Breakthrough;
using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

/// <summary>
/// Auricrux breakthrough APIs — self-correction loop demos owned by this app.
/// </summary>
[ApiController]
[Route("api/breakthrough")]
public sealed class BreakthroughController(
    FoundationPourDemoService foundationPourDemo,
    ILogger<BreakthroughController> logger) : ControllerBase
{
    /// <summary>
    /// Run the foundation-pour self-correction loop:
    /// competing hypotheses → divergent field measurements → verification → meta-learning → proof.
    /// Works without Atlas (in-memory cache).
    /// </summary>
    [HttpPost("demo/foundation-pour")]
    public async Task<ActionResult<FoundationPourDemoResult>> RunFoundationPourDemo(
        [FromBody] FoundationPourDemoOptions? options,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Foundation pour breakthrough demo requested");
        var result = await foundationPourDemo.RunAsync(options, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Convenience GET for browser smoke checks (same demo, default options).
    /// </summary>
    [HttpGet("demo/foundation-pour")]
    public async Task<ActionResult<FoundationPourDemoResult>> GetFoundationPourDemo(
        CancellationToken cancellationToken)
    {
        var result = await foundationPourDemo.RunAsync(null, cancellationToken);
        return Ok(result);
    }
}

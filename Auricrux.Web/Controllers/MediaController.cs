using Auricrux.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

[ApiController]
[Route("api/media")]
public sealed class MediaController(MediaGenerationService media) : ControllerBase
{
    [HttpPost("image")]
    public async Task<ActionResult<MediaArtifact>> Image([FromBody] MediaPromptRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { error = "Prompt is required." });
        }

        var artifact = await media.GenerateImageAsync(request.Prompt.Trim(), cancellationToken);
        return Ok(artifact);
    }

    [HttpPost("video")]
    public async Task<ActionResult<MediaArtifact>> Video([FromBody] MediaPromptRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { error = "Prompt is required." });
        }

        var artifact = await media.GenerateVideoAsync(request.Prompt.Trim(), request.Frames ?? 8, cancellationToken);
        return Ok(artifact);
    }
}

public sealed class MediaPromptRequest
{
    public string? Prompt { get; set; }
    public int? Frames { get; set; }
}

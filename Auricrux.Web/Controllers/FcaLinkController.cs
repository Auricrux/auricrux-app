using Auricrux.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

[ApiController]
[Route("api/account")]
public sealed class FcaLinkController(FcaAccountLinkService fcaLinks) : ControllerBase
{
    [HttpPost("{email}/link-fca")]
    public async Task<ActionResult<FcaLinkRecord>> Link(
        string email,
        [FromBody] LinkFcaRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FcaBearerToken))
        {
            return BadRequest(new { error = "FcaBearerToken is required (FCA ecosystem JWT / main-token)." });
        }

        try
        {
            var link = await fcaLinks.LinkAsync(email, request.FcaBearerToken, cancellationToken);
            return Ok(link);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{email}/link-fca")]
    public ActionResult<FcaLinkRecord> GetLink(string email)
    {
        var link = fcaLinks.Get(email);
        return link is null ? NotFound() : Ok(link);
    }

    [HttpDelete("{email}/link-fca")]
    public IActionResult Unlink(string email)
        => fcaLinks.Unlink(email) ? NoContent() : NotFound();
}

public sealed class LinkFcaRequest
{
    public string? FcaBearerToken { get; set; }
}

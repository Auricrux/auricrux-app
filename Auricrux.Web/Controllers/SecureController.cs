using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

/// <summary>
/// Minimal authenticated probe used to prove AUX-021: when Auth:Enabled is true and an
/// Authority is configured, this endpoint is default-deny (401 without a valid bearer token).
/// When Auth:Enabled is false (default dev mode), it stays open — anonymous-by-default is an
/// explicit, documented configuration choice rather than an oversight.
///
/// The gate is a manual, request-time check against the live <see cref="IConfiguration"/>
/// (rather than a static [Authorize] attribute) so that: (1) hosts that never configure any
/// authentication scheme don't crash with "No authenticationScheme was specified" when the
/// endpoint metadata is inspected, and (2) the check always reflects the actual current
/// configuration, including configuration layered on after the app builder ran (as
/// WebApplicationFactory-based integration tests do).
/// </summary>
[ApiController]
[Route("api/secure")]
public sealed class SecureController : ControllerBase
{
    [HttpGet("ping")]
    public ActionResult<object> Ping([FromServices] IConfiguration config)
    {
        var authEnabled = config.GetValue("Auth:Enabled", false);
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;

        if (authEnabled && !isAuthenticated)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        return Ok(new
        {
            authenticated = isAuthenticated,
            name = User.Identity?.Name
        });
    }
}

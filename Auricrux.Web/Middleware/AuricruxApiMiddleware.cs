namespace Auricrux.Web.Middleware;

/// <summary>
/// Security headers and request logging for API routes.
/// </summary>
public sealed class AuricruxApiMiddleware(RequestDelegate next, ILogger<AuricruxApiMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ApplySecurityHeaders(context);

        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("API {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        await next(context);
    }

    private static void ApplySecurityHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(self), geolocation=()";
        headers["X-XSS-Protection"] = "0";
        if (!headers.ContainsKey("Content-Security-Policy"))
        {
            headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; connect-src 'self' ws: wss:; frame-ancestors 'none';";
        }
    }
}

public static class AuricruxApiMiddlewareExtensions
{
    public static IApplicationBuilder UseAuricruxApiMiddleware(this IApplicationBuilder builder)
        => builder.UseMiddleware<AuricruxApiMiddleware>();
}

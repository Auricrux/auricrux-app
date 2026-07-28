using System.Diagnostics;
using System.Text.Json;

namespace Auricrux.Web.Middleware;

/// <summary>
/// Returns structured JSON errors for unhandled exceptions on API routes instead of HTML error pages.
/// </summary>
public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger, IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                throw;
            }

            logger.LogError(ex, "Unhandled API exception on {Method} {Path} (correlation={CorrelationId})",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var payload = new
            {
                error = "An unexpected error occurred processing the request.",
                correlationId = context.TraceIdentifier,
                detail = env.IsDevelopment() ? ex.Message : null
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}

public static class ApiExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseApiExceptionHandling(this IApplicationBuilder builder)
        => builder.UseMiddleware<ApiExceptionMiddleware>();
}

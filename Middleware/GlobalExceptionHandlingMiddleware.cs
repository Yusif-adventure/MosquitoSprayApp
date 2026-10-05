using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace SmartMosquitoControl.Middleware;

/// <summary>
/// Turns unhandled exceptions under /api into a small JSON error with a real status code.
/// Page requests are deliberately re-thrown so <c>UseExceptionHandler</c> renders the error page
/// with a genuine HTTP 500 (rather than a 302 redirect that hides failures from monitoring).
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        // External OAuth cancelled / denied by the user (e.g. Google "Cancel" button).
        // This is a normal user action, not a server fault — send them back to the login page
        // instead of letting the exception bubble up to the error page as a 500.
        catch (AuthenticationFailureException ex) when (!context.Response.HasStarted)
        {
            _logger.LogInformation(ex, "External authentication was denied or failed for {Path}", context.Request.Path);

            context.Response.Redirect("/Account/Login?error=ExternalLoginCanceled");
        }
        catch (Exception ex) when (context.Request.Path.StartsWithSegments("/api") && !context.Response.HasStarted)
        {
            _logger.LogError(ex, "Unhandled exception in API request {Path}", context.Request.Path);

            var (status, message) = ex switch
            {
                ArgumentException => (HttpStatusCode.BadRequest, "Invalid request parameters"),
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "You are not authorized to perform this action"),
                _ => (HttpStatusCode.InternalServerError, "An error occurred while processing your request")
            };

            context.Response.Clear();
            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(
                new { statusCode = (int)status, message }, JsonOptions));
        }
    }
}

public static class GlobalExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }
}
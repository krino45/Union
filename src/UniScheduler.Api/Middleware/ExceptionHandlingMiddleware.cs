using System.Text.Json;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Models;

namespace UniScheduler.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var (status, title, detail, extra) = exception switch
        {
            ValidationException ve => (400, "Validation Failed", ve.Message, (object)ve.Errors),
            NotFoundException nfe => (404, "Not Found", nfe.Message, (object?)null),
            ForbiddenException fe => (403, "Forbidden", fe.Message, (object?)null),
            ConflictException ce => (409, "Schedule Conflict", ce.Message, (object)ce.Conflicts),
            UnauthorizedAccessException uae => (401, "Unauthorized", uae.Message, (object?)null),
            InvalidOperationException ioe => (400, "Bad Request", ioe.Message, (object?)null),
            _ => (500, "Internal Server Error", "An unexpected error occurred.", (object?)null)
        };

        context.Response.StatusCode = status;

        var problem = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.com/{status}",
            ["title"] = title,
            ["status"] = status,
            ["detail"] = detail,
        };
        if (extra != null) problem["errors"] = extra;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}

using AccessRequestHub.Api.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AccessRequestHub.Api.Infrastructure;

/// <summary>Maps DomainException subtypes to ProblemDetails with a stable "code" field,
/// so clients can distinguish validation (422), forbidden (403), not found (404), and conflict (409).</summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            UnauthenticatedException => StatusCodes.Status401Unauthorized,
            ResourceNotFoundException => StatusCodes.Status404NotFound,
            ForbiddenException => StatusCodes.Status403Forbidden,
            ValidationException => StatusCodes.Status422UnprocessableEntity,
            InvalidStateException or VersionConflictException or IdempotencyMismatchException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        var code = exception is DomainException domainException ? domainException.Code : "internal_error";
        var correlationId = httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemsKey, out var cid)
            ? cid?.ToString()
            : null;

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Request rejected with {Code} ({StatusCode}) on {Method} {Path}: {Message}",
                code, statusCode, httpContext.Request.Method, httpContext.Request.Path, exception.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = code,
            Detail = statusCode == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred."
                : exception.Message,
        };
        problemDetails.Extensions["code"] = code;
        if (correlationId is not null)
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}

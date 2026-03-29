using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BankingApi.Application.Common.Exceptions;

/// <summary>
/// Global IExceptionHandler (.NET 8+) that converts domain exceptions
/// into RFC 7807 ProblemDetails responses.
///
/// Registration in Program.cs:
///   builder.Services.AddExceptionHandler&lt;GlobalExceptionHandler&gt;();
///   builder.Services.AddProblemDetails();
///   app.UseExceptionHandler();
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, errors) = MapException(exception);

        // Log server errors at Error level; client errors at Warning
        if (status >= 500)
            _logger.LogError(exception,
                "Unhandled exception: {Message}", exception.Message);
        else
            _logger.LogWarning(exception,
                "Handled exception [{Status}]: {Message}", status, exception.Message);

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7807",
            Title = title,
            Status = status,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        // Attach field-level errors for validation failures (422)
        if (errors is not null)
            problem.Extensions["errors"] = errors;

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response
            .WriteAsJsonAsync(problem, cancellationToken);

        return true; // Exception is handled — suppress default middleware
    }

    // ── Mapping table ─────────────────────────────────────────────────────────

    private static (int Status, string Title, IDictionary<string, string[]>? Errors)
        MapException(Exception exception) => exception switch
        {
            InsufficientFundsException =>
                (StatusCodes.Status400BadRequest,
                 "Insufficient Funds",
                 null),

            ValidationException ve =>
                (StatusCodes.Status422UnprocessableEntity,
                 "Validation Failed",
                 ve.Errors),

            NotFoundException =>
                (StatusCodes.Status404NotFound,
                 "Resource Not Found",
                 null),

            UnauthorizedException =>
                (StatusCodes.Status401Unauthorized,
                 "Unauthorized",
                 null),

            _ =>
                (StatusCodes.Status500InternalServerError,
                 "An unexpected error occurred.",
                 null)
        };
}
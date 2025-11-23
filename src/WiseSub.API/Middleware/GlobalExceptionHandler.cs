using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WiseSub.API.Middleware;

/// <summary>
/// Global exception handler that implements IExceptionHandler interface.
/// 
/// IMPORTANT: This handler is designed to catch ONLY unexpected/unhandled exceptions.
/// The application uses the Result pattern for expected errors and business logic failures.
/// 
/// Expected errors (authentication failures, validation errors, not found, etc.) should be
/// returned as Result.Failure() with appropriate error messages, not thrown as exceptions.
/// 
/// This handler will catch:
/// - Database connection failures
/// - Unexpected runtime errors
/// - Third-party API failures that weren't handled
/// - Programming errors (null reference, index out of range, etc.)
/// 
/// Returns appropriate HTTP responses based on exception type with detailed logging.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Exception occurred: {Message}",
            exception.Message);

        var problemDetails = CreateProblemDetails(httpContext, exception);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private ProblemDetails CreateProblemDetails(HttpContext httpContext, Exception exception)
    {
        var statusCode = StatusCodes.Status500InternalServerError;
        var title = "Internal Server Error";

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path,
            Type = $"https://httpstatuses.io/{statusCode}"
        };

        // Add exception type for debugging
        problemDetails.Extensions["exceptionType"] = exception.GetType().Name;

        return problemDetails;
    }
}

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
/// In production, exception details are hidden to prevent information disclosure.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
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
            Instance = httpContext.Request.Path,
            Type = $"https://httpstatuses.io/{statusCode}"
        };

        // Only expose exception details in development to prevent information disclosure
        if (_environment.IsDevelopment())
        {
            problemDetails.Detail = exception.Message;
            problemDetails.Extensions["exceptionType"] = exception.GetType().Name;
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }
        else
        {
            problemDetails.Detail = "An unexpected error occurred. Please try again later.";
        }

        return problemDetails;
    }
}

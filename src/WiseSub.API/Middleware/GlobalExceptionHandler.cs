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
/// Includes correlation IDs for request tracing across distributed systems.
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
        var correlationId = GetCorrelationId(httpContext);
        
        _logger.LogError(
            exception,
            "Exception occurred. CorrelationId: {CorrelationId}, Path: {Path}, Message: {Message}",
            correlationId,
            httpContext.Request.Path,
            exception.Message);

        var problemDetails = CreateProblemDetails(httpContext, exception, correlationId);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private ProblemDetails CreateProblemDetails(HttpContext httpContext, Exception exception, string correlationId)
    {
        var (statusCode, errorCode, title) = MapExceptionToResponse(exception);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Instance = httpContext.Request.Path,
            Type = $"https://httpstatuses.io/{statusCode}"
        };

        // Always include correlation ID and timestamp for tracing
        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow.ToString("O");
        problemDetails.Extensions["errorCode"] = errorCode;

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

    private static (int StatusCode, string ErrorCode, string Title) MapExceptionToResponse(Exception exception)
    {
        return exception switch
        {
            ArgumentNullException => (StatusCodes.Status400BadRequest, "INVALID_ARGUMENT", "Bad Request"),
            ArgumentException => (StatusCodes.Status400BadRequest, "INVALID_ARGUMENT", "Bad Request"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Unauthorized"),
            InvalidOperationException => (StatusCodes.Status400BadRequest, "INVALID_OPERATION", "Invalid Operation"),
            TimeoutException => (StatusCodes.Status504GatewayTimeout, "TIMEOUT", "Gateway Timeout"),
            HttpRequestException => (StatusCodes.Status502BadGateway, "EXTERNAL_SERVICE_ERROR", "External Service Error"),
            OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "REQUEST_CANCELLED", "Request Cancelled"),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "Internal Server Error")
        };
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue("CorrelationId", out var correlationId) && correlationId is string id)
        {
            return id;
        }
        
        return Guid.NewGuid().ToString("N")[..12];
    }
}

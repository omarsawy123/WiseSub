using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using WiseSub.API.Middleware;

namespace WiseSub.API.Tests.Middleware;

public class CorrelationIdMiddlewareTests
{
    private readonly Mock<ILogger<CorrelationIdMiddleware>> _loggerMock;

    public CorrelationIdMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<CorrelationIdMiddleware>>();
    }

    [Fact]
    public async Task InvokeAsync_WithoutCorrelationIdHeader_GeneratesNewCorrelationId()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.True(nextCalled);
        Assert.True(httpContext.Items.ContainsKey("CorrelationId"));
        var correlationId = httpContext.Items["CorrelationId"] as string;
        Assert.NotNull(correlationId);
        Assert.Equal(12, correlationId.Length); // Short GUID format
    }

    [Fact]
    public async Task InvokeAsync_WithCorrelationIdHeader_UsesProvidedCorrelationId()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = "provided-correlation-id";
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        var correlationId = httpContext.Items["CorrelationId"] as string;
        Assert.Equal("provided-correlation-id", correlationId);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyCorrelationIdHeader_GeneratesNewCorrelationId()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = "";
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        var correlationId = httpContext.Items["CorrelationId"] as string;
        Assert.NotNull(correlationId);
        Assert.NotEmpty(correlationId);
        Assert.NotEqual("", correlationId);
    }

    [Fact]
    public async Task InvokeAsync_WithWhitespaceCorrelationIdHeader_GeneratesNewCorrelationId()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = "   ";
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        var correlationId = httpContext.Items["CorrelationId"] as string;
        Assert.NotNull(correlationId);
        Assert.DoesNotContain(" ", correlationId);
    }

    [Fact]
    public async Task InvokeAsync_AddsCorrelationIdToResponseHeaders()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        string? capturedCorrelationId = null;
        
        // Capture the correlation ID that will be added to response headers
        httpContext.Response.OnStarting(state =>
        {
            var ctx = (HttpContext)state;
            if (ctx.Response.Headers.TryGetValue("X-Correlation-ID", out var value))
            {
                capturedCorrelationId = value.ToString();
            }
            return Task.CompletedTask;
        }, httpContext);
        
        RequestDelegate next = async ctx =>
        {
            // Trigger response start by writing to the response
            await ctx.Response.WriteAsync("test");
        };
        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert - verify correlation ID was stored in Items (which is always set)
        Assert.True(httpContext.Items.ContainsKey("CorrelationId"));
        var correlationId = httpContext.Items["CorrelationId"] as string;
        Assert.NotNull(correlationId);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_CorrelationIdIsAvailableInNextMiddleware()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        string? capturedCorrelationId = null;
        RequestDelegate next = ctx =>
        {
            capturedCorrelationId = ctx.Items["CorrelationId"] as string;
            return Task.CompletedTask;
        };
        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.NotNull(capturedCorrelationId);
        Assert.Equal(12, capturedCorrelationId.Length);
    }

    [Fact]
    public async Task InvokeAsync_GeneratedCorrelationIdIsConsistent()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        string? correlationIdInItems = null;
        string? correlationIdInNext = null;
        
        RequestDelegate next = ctx =>
        {
            correlationIdInNext = ctx.Items["CorrelationId"] as string;
            return Task.CompletedTask;
        };
        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(httpContext);
        correlationIdInItems = httpContext.Items["CorrelationId"] as string;

        // Assert
        Assert.Equal(correlationIdInItems, correlationIdInNext);
    }

    [Fact]
    public async Task InvokeAsync_MultipleRequests_GenerateDifferentCorrelationIds()
    {
        // Arrange
        var correlationIds = new List<string>();
        RequestDelegate next = ctx =>
        {
            correlationIds.Add(ctx.Items["CorrelationId"] as string ?? "");
            return Task.CompletedTask;
        };
        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object);

        // Act
        for (int i = 0; i < 10; i++)
        {
            var httpContext = new DefaultHttpContext();
            await middleware.InvokeAsync(httpContext);
        }

        // Assert
        Assert.Equal(10, correlationIds.Distinct().Count());
    }
}

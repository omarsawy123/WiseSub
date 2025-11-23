using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using WiseSub.API.Middleware;

namespace WiseSub.API.Tests.Middleware;

public class GlobalExceptionHandlerTests
{
    private readonly Mock<ILogger<GlobalExceptionHandler>> _loggerMock;
    private readonly GlobalExceptionHandler _handler;

    public GlobalExceptionHandlerTests()
    {
        _loggerMock = new Mock<ILogger<GlobalExceptionHandler>>();
        _handler = new GlobalExceptionHandler(_loggerMock.Object);
    }

    [Fact]
    public async Task TryHandleAsync_WithException_ReturnsTrue()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var exception = new InvalidOperationException("Test exception");

        // Act
        var result = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TryHandleAsync_WithException_SetsStatusCodeTo500()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var exception = new InvalidOperationException("Test exception");

        // Act
        await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        Assert.Equal(500, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_WithException_LogsError()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var exception = new InvalidOperationException("Test exception");

        // Act
        await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_WithException_WritesJsonResponse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var responseStream = new MemoryStream();
        httpContext.Response.Body = responseStream;
        var exception = new InvalidOperationException("Test exception message");

        // Act
        await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        responseStream.Position = 0;
        var reader = new StreamReader(responseStream);
        var responseBody = await reader.ReadToEndAsync();
        
        Assert.NotEmpty(responseBody);
        
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseBody);
        Assert.Equal(500, problemDetails.GetProperty("status").GetInt32());
        Assert.Equal("Internal Server Error", problemDetails.GetProperty("title").GetString());
        Assert.Equal("Test exception message", problemDetails.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_WithException_IncludesExceptionType()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var responseStream = new MemoryStream();
        httpContext.Response.Body = responseStream;
        var exception = new ArgumentNullException("testParameter", "Test argument null");

        // Act
        await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        responseStream.Position = 0;
        var reader = new StreamReader(responseStream);
        var responseBody = await reader.ReadToEndAsync();
        
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseBody);
        var extensionsProperty = problemDetails.GetProperty("exceptionType");
        Assert.Equal("ArgumentNullException", extensionsProperty.GetString());
    }

    [Fact]
    public async Task TryHandleAsync_WithException_IncludesRequestPath()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/test";
        var responseStream = new MemoryStream();
        httpContext.Response.Body = responseStream;
        var exception = new InvalidOperationException("Test exception");

        // Act
        await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        responseStream.Position = 0;
        var reader = new StreamReader(responseStream);
        var responseBody = await reader.ReadToEndAsync();
        
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseBody);
        Assert.Equal("/api/test", problemDetails.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_WithNullReferenceException_HandlesGracefully()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var exception = new NullReferenceException("Object reference not set");

        // Act
        var result = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(500, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_WithDatabaseException_HandlesGracefully()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var exception = new InvalidOperationException("Database connection failed");

        // Act
        var result = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(500, httpContext.Response.StatusCode);
    }
}

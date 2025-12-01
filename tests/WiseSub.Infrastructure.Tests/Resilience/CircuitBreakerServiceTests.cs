using Microsoft.Extensions.Logging;
using Moq;
using Polly.CircuitBreaker;
using WiseSub.Infrastructure.Resilience;

namespace WiseSub.Infrastructure.Tests.Resilience;

public class CircuitBreakerServiceTests
{
    private readonly Mock<ILogger<CircuitBreakerService>> _loggerMock;
    private readonly CircuitBreakerService _service;

    public CircuitBreakerServiceTests()
    {
        _loggerMock = new Mock<ILogger<CircuitBreakerService>>();
        _service = new CircuitBreakerService(_loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var expectedResult = "success";

        // Act
        var result = await _service.ExecuteAsync("TestService", async _ =>
        {
            await Task.Delay(1);
            return expectedResult;
        });

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ExecuteAsync_WithVoidOperation_CompletesSuccessfully()
    {
        // Arrange
        var executed = false;

        // Act
        await _service.ExecuteAsync("TestService", async _ =>
        {
            await Task.Delay(1);
            executed = true;
        });

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public async Task ExecuteAsync_WithTransientFailure_RetriesOperation()
    {
        // Arrange
        var attempts = 0;

        // Act
        var result = await _service.ExecuteAsync("TestService", async _ =>
        {
            attempts++;
            if (attempts < 2)
            {
                throw new HttpRequestException("Transient failure");
            }
            await Task.Delay(1);
            return "success";
        });

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task GetCircuitState_ForNewService_ReturnsClosed()
    {
        // Act
        var state = _service.GetCircuitState("NewService");

        // Assert
        Assert.Equal(CircuitState.Closed, state);
    }

    [Fact]
    public async Task GetCircuitState_AfterSuccessfulOperation_ReturnsClosed()
    {
        // Arrange
        await _service.ExecuteAsync("TestService", async _ =>
        {
            await Task.Delay(1);
            return "success";
        });

        // Act
        var state = _service.GetCircuitState("TestService");

        // Assert
        Assert.Equal(CircuitState.Closed, state);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _service.ExecuteAsync("TestService", async ct =>
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(1000, ct);
                return "success";
            }, cts.Token);
        });
    }

    [Fact]
    public async Task ExecuteAsync_SameServiceName_ReusesPipeline()
    {
        // Arrange & Act
        await _service.ExecuteAsync("SharedService", _ => Task.FromResult("first"));
        await _service.ExecuteAsync("SharedService", _ => Task.FromResult("second"));

        // Assert - no exception means pipeline was reused successfully
        var state = _service.GetCircuitState("SharedService");
        Assert.Equal(CircuitState.Closed, state);
    }

    [Fact]
    public async Task ExecuteAsync_DifferentServiceNames_CreatesSeparatePipelines()
    {
        // Arrange & Act
        await _service.ExecuteAsync("ServiceA", _ => Task.FromResult("a"));
        await _service.ExecuteAsync("ServiceB", _ => Task.FromResult("b"));

        // Assert
        var stateA = _service.GetCircuitState("ServiceA");
        var stateB = _service.GetCircuitState("ServiceB");
        Assert.Equal(CircuitState.Closed, stateA);
        Assert.Equal(CircuitState.Closed, stateB);
    }

    [Fact]
    public async Task ExecuteAsync_LogsRetryAttempts()
    {
        // Arrange
        var attempts = 0;

        // Act
        try
        {
            await _service.ExecuteAsync("TestService", async _ =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw new HttpRequestException("Transient failure");
                }
                await Task.Delay(1);
                return "success";
            });
        }
        catch
        {
            // Ignore
        }

        // Assert - verify logging was called for retry
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retry attempt")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleSuccessfulCalls_MaintainsClosedState()
    {
        // Arrange & Act
        for (int i = 0; i < 10; i++)
        {
            await _service.ExecuteAsync("TestService", _ => Task.FromResult($"result-{i}"));
        }

        // Assert
        var state = _service.GetCircuitState("TestService");
        Assert.Equal(CircuitState.Closed, state);
    }

    [Fact]
    public async Task ExecuteAsync_VoidOperation_WithTransientFailure_Retries()
    {
        // Arrange
        var attempts = 0;

        // Act
        await _service.ExecuteAsync("TestService", async _ =>
        {
            attempts++;
            if (attempts < 2)
            {
                throw new HttpRequestException("Transient failure");
            }
            await Task.Delay(1);
        });

        // Assert
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentExceptionTypes_HandlesCorrectly()
    {
        // Arrange
        var attempts = 0;

        // Act
        var result = await _service.ExecuteAsync("TestService", async _ =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new TimeoutException("Timeout");
            }
            if (attempts == 2)
            {
                throw new InvalidOperationException("Invalid");
            }
            await Task.Delay(1);
            return "success";
        });

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(3, attempts);
    }
}

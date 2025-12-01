using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace WiseSub.Infrastructure.Resilience;

/// <summary>
/// Provides circuit breaker and retry policies for external service calls.
/// Implements the circuit breaker pattern to prevent cascading failures when external services are unavailable.
/// 
/// Circuit breaker states:
/// - Closed: Normal operation, requests pass through
/// - Open: Service is failing, requests are rejected immediately
/// - Half-Open: Testing if service has recovered
/// 
/// Configuration:
/// - Opens after 3 consecutive failures
/// - Stays open for 30 seconds before testing recovery
/// - Retry with exponential backoff: 1s, 5s, 15s
/// </summary>
public interface ICircuitBreakerService
{
    /// <summary>
    /// Executes an async operation with circuit breaker and retry protection.
    /// </summary>
    Task<T> ExecuteAsync<T>(string serviceName, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes an async operation with circuit breaker and retry protection (no return value).
    /// </summary>
    Task ExecuteAsync(string serviceName, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the current state of the circuit breaker for a service.
    /// </summary>
    CircuitState GetCircuitState(string serviceName);
}

public class CircuitBreakerService : ICircuitBreakerService
{
    private readonly ILogger<CircuitBreakerService> _logger;
    private readonly Dictionary<string, ResiliencePipeline<object>> _pipelines = new();
    private readonly Dictionary<string, CircuitBreakerStateProvider> _stateProviders = new();
    private readonly object _lock = new();

    // Configuration constants
    private const int FailureThreshold = 3;
    private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15) };

    public CircuitBreakerService(ILogger<CircuitBreakerService> logger)
    {
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(string serviceName, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var pipeline = GetOrCreatePipeline(serviceName);
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            var value = await operation(ct);
            return (object)value!;
        }, cancellationToken);
        
        return (T)result!;
    }

    public async Task ExecuteAsync(string serviceName, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        var pipeline = GetOrCreatePipeline(serviceName);
        await pipeline.ExecuteAsync(async ct =>
        {
            await operation(ct);
            return new object();
        }, cancellationToken);
    }

    public CircuitState GetCircuitState(string serviceName)
    {
        lock (_lock)
        {
            if (_stateProviders.TryGetValue(serviceName, out var provider))
            {
                return provider.CircuitState;
            }
            return CircuitState.Closed;
        }
    }

    private ResiliencePipeline<object> GetOrCreatePipeline(string serviceName)
    {
        lock (_lock)
        {
            if (_pipelines.TryGetValue(serviceName, out var existingPipeline))
            {
                return existingPipeline;
            }

            var stateProvider = new CircuitBreakerStateProvider();
            _stateProviders[serviceName] = stateProvider;

            var pipeline = new ResiliencePipelineBuilder<object>()
                .AddRetry(new RetryStrategyOptions<object>
                {
                    MaxRetryAttempts = RetryDelays.Length,
                    DelayGenerator = args =>
                    {
                        var delay = args.AttemptNumber < RetryDelays.Length 
                            ? RetryDelays[args.AttemptNumber] 
                            : RetryDelays[^1];
                        return ValueTask.FromResult<TimeSpan?>(delay);
                    },
                    OnRetry = args =>
                    {
                        _logger.LogWarning(
                            "Retry attempt {AttemptNumber} for {ServiceName} after {Delay}ms. Exception: {Exception}",
                            args.AttemptNumber + 1,
                            serviceName,
                            args.RetryDelay.TotalMilliseconds,
                            args.Outcome.Exception?.Message ?? "Unknown");
                        return ValueTask.CompletedTask;
                    },
                    ShouldHandle = new PredicateBuilder<object>().Handle<Exception>(ex => 
                        ex is not OperationCanceledException && 
                        ex is not BrokenCircuitException)
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions<object>
                {
                    FailureRatio = 1.0, // 100% failure ratio (all attempts must fail)
                    MinimumThroughput = FailureThreshold,
                    SamplingDuration = TimeSpan.FromMinutes(1),
                    BreakDuration = BreakDuration,
                    OnOpened = args =>
                    {
                        stateProvider.CircuitState = CircuitState.Open;
                        _logger.LogError(
                            "Circuit breaker OPENED for {ServiceName}. Service will be unavailable for {Duration}s. Exception: {Exception}",
                            serviceName,
                            BreakDuration.TotalSeconds,
                            args.Outcome.Exception?.Message ?? "Unknown");
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        stateProvider.CircuitState = CircuitState.Closed;
                        _logger.LogInformation(
                            "Circuit breaker CLOSED for {ServiceName}. Service is now available.",
                            serviceName);
                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = args =>
                    {
                        stateProvider.CircuitState = CircuitState.HalfOpen;
                        _logger.LogInformation(
                            "Circuit breaker HALF-OPEN for {ServiceName}. Testing if service has recovered.",
                            serviceName);
                        return ValueTask.CompletedTask;
                    },
                    ShouldHandle = new PredicateBuilder<object>().Handle<Exception>(ex => 
                        ex is not OperationCanceledException)
                })
                .Build();

            _pipelines[serviceName] = pipeline;
            return pipeline;
        }
    }
}

/// <summary>
/// Helper class to track circuit breaker state.
/// </summary>
public class CircuitBreakerStateProvider
{
    public CircuitState CircuitState { get; set; } = CircuitState.Closed;
}

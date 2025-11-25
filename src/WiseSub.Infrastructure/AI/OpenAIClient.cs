using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using WiseSub.Application.Common.Interfaces;

namespace WiseSub.Infrastructure.AI;

/// <summary>
/// Implementation of OpenAI client for AI extraction with retry logic and rate limiting
/// </summary>
public class OpenAIClient : IOpenAIClient
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<OpenAIClient> _logger;
    private readonly string _model;
    private readonly int _maxRetries;
    private readonly int _initialRetryDelayMs;
    private readonly SemaphoreSlim _rateLimiter = new(10); // Max 10 concurrent requests

    public OpenAIClient(IConfiguration configuration, ILogger<OpenAIClient> logger)
    {
        _logger = logger;
        
        var apiKey = configuration["OpenAI:ApiKey"] 
            ?? throw new InvalidOperationException("OpenAI API key not configured");
        
        _model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        _maxRetries = configuration.GetValue<int>("OpenAI:MaxRetries", 3);
        _initialRetryDelayMs = configuration.GetValue<int>("OpenAI:InitialRetryDelayMs", 1000);
        
        var openAIClient = new OpenAI.OpenAIClient(apiKey);
        _chatClient = openAIClient.GetChatClient(_model);
        
        _logger.LogInformation("OpenAI client initialized with model: {Model}, MaxRetries: {MaxRetries}", _model, _maxRetries);
    }

    public async Task<string> GetCompletionAsync(
        string systemPrompt,
        string userPrompt,
        double temperature = 0.1,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Sending completion request to OpenAI");

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = (float)temperature
            };

            var completion = await _chatClient.CompleteChatAsync(
                messages,
                options,
                cancellationToken);

            var response = completion.Value.Content[0].Text;
            
            _logger.LogDebug("Received completion response from OpenAI");
            
            return response;
        }, cancellationToken);
    }

    public async Task<T?> GetJsonCompletionAsync<T>(
        string systemPrompt,
        string userPrompt,
        double temperature = 0.1,
        CancellationToken cancellationToken = default) where T : class
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Sending JSON completion request to OpenAI for type {Type}", typeof(T).Name);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = (float)temperature,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var completion = await _chatClient.CompleteChatAsync(
                messages,
                options,
                cancellationToken);

            var jsonResponse = completion.Value.Content[0].Text;
            
            _logger.LogDebug("Received JSON completion response from OpenAI");

            var result = JsonSerializer.Deserialize<T>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result;
        }, cancellationToken);
    }

    /// <summary>
    /// Executes an operation with retry logic and rate limiting
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            var retryCount = 0;
            var delay = _initialRetryDelayMs;

            while (true)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (IsRetryableException(ex) && retryCount < _maxRetries)
                {
                    retryCount++;
                    _logger.LogWarning(
                        ex,
                        "OpenAI request failed (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms",
                        retryCount, _maxRetries, delay);

                    await Task.Delay(delay, cancellationToken);
                    
                    // Exponential backoff with jitter
                    delay = (int)(delay * 2 + Random.Shared.Next(100, 500));
                }
            }
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    /// <summary>
    /// Determines if an exception is retryable (rate limits, transient errors)
    /// </summary>
    private static bool IsRetryableException(Exception ex)
    {
        // Retry on HTTP errors that indicate rate limiting or transient failures
        if (ex is HttpRequestException httpEx)
        {
            return true; // Retry all HTTP errors
        }

        // Retry on timeout
        if (ex is TaskCanceledException && !((TaskCanceledException)ex).CancellationToken.IsCancellationRequested)
        {
            return true;
        }

        // Check for OpenAI-specific rate limit errors in the message
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("rate limit") || 
               message.Contains("429") || 
               message.Contains("503") ||
               message.Contains("timeout");
    }
}

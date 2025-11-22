using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using WiseSub.Application.Common.Interfaces;

namespace WiseSub.Infrastructure.AI;

/// <summary>
/// Implementation of OpenAI client for AI extraction
/// </summary>
public class OpenAIClient : IOpenAIClient
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<OpenAIClient> _logger;
    private readonly string _model;

    public OpenAIClient(IConfiguration configuration, ILogger<OpenAIClient> logger)
    {
        _logger = logger;
        
        var apiKey = configuration["OpenAI:ApiKey"] 
            ?? throw new InvalidOperationException("OpenAI API key not configured");
        
        _model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        
        var openAIClient = new OpenAI.OpenAIClient(apiKey);
        _chatClient = openAIClient.GetChatClient(_model);
        
        _logger.LogInformation("OpenAI client initialized with model: {Model}", _model);
    }

    public async Task<string> GetCompletionAsync(
        string systemPrompt,
        string userPrompt,
        double temperature = 0.1,
        CancellationToken cancellationToken = default)
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI API");
            throw;
        }
    }

    public async Task<T?> GetJsonCompletionAsync<T>(
        string systemPrompt,
        string userPrompt,
        double temperature = 0.1,
        CancellationToken cancellationToken = default) where T : class
    {
        try
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
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing OpenAI JSON response");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI API for JSON completion");
            throw;
        }
    }
}

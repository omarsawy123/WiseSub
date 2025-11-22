namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Client for interacting with OpenAI API
/// </summary>
public interface IOpenAIClient
{
    /// <summary>
    /// Sends a completion request to OpenAI
    /// </summary>
    /// <param name="systemPrompt">System prompt for the model</param>
    /// <param name="userPrompt">User prompt with the content to analyze</param>
    /// <param name="temperature">Temperature for response generation (0.0 to 1.0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The model's response</returns>
    Task<string> GetCompletionAsync(
        string systemPrompt,
        string userPrompt,
        double temperature = 0.1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a completion request expecting JSON response
    /// </summary>
    /// <typeparam name="T">Type to deserialize the JSON response to</typeparam>
    /// <param name="systemPrompt">System prompt for the model</param>
    /// <param name="userPrompt">User prompt with the content to analyze</param>
    /// <param name="temperature">Temperature for response generation (0.0 to 1.0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deserialized response object</returns>
    Task<T?> GetJsonCompletionAsync<T>(
        string systemPrompt,
        string userPrompt,
        double temperature = 0.1,
        CancellationToken cancellationToken = default) where T : class;
}

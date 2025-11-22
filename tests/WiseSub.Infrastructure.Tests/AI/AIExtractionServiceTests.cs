using Microsoft.Extensions.Logging;
using Moq;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Infrastructure.AI;
using Xunit;

namespace WiseSub.Infrastructure.Tests.AI;

/// <summary>
/// Basic tests for AI Extraction Service
/// Note: Full integration tests with actual OpenAI API calls require a valid API key
/// and are best performed as manual integration tests or with recorded responses.
/// The AI extraction service is designed to work with OpenAI's GPT-4o-mini model
/// and handles email classification and subscription data extraction.
/// </summary>
public class AIExtractionServiceTests
{
    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var mockOpenAIClient = new Mock<IOpenAIClient>();
        var mockLogger = new Mock<ILogger<AIExtractionService>>();

        // Act
        var service = new AIExtractionService(mockOpenAIClient.Object, mockLogger.Object);

        // Assert
        Assert.NotNull(service);
    }
}

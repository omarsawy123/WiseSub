using System.Net;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Infrastructure.Authentication;
using Xunit;

namespace WiseSub.Infrastructure.Tests.Authentication;

public class GoogleAuthenticationServiceTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;

    public GoogleAuthenticationServiceTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockUserService = new Mock<IUserService>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        // Setup default configuration values
        _mockConfiguration.Setup(c => c["Authentication:JwtSecret"])
            .Returns("ThisIsASecretKeyForTestingPurposesOnly12345678901234567890");
        _mockConfiguration.Setup(c => c["Authentication:JwtIssuer"])
            .Returns("WiseSub");
        _mockConfiguration.Setup(c => c["Authentication:JwtAudience"])
            .Returns("WiseSub");
        _mockConfiguration.Setup(c => c["Authentication:Google:ClientId"])
            .Returns("test-client-id");
        _mockConfiguration.Setup(c => c["Authentication:Google:ClientSecret"])
            .Returns("test-client-secret");
        _mockConfiguration.Setup(c => c["Authentication:Google:RedirectUri"])
            .Returns("http://localhost:3000/auth/callback");
    }

    [Fact]
    public async Task RevokeTokenAsync_ShouldReturnTrue_WhenRevocationSucceeds()
    {
        // Arrange
        var refreshToken = "valid-refresh-token";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == "https://oauth2.googleapis.com/revoke"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var service = new GoogleAuthenticationService(
            _mockConfiguration.Object,
            _mockUserService.Object,
            _mockHttpClientFactory.Object);

        // Act
        var result = await service.RevokeTokenAsync(refreshToken);

        // Assert
        Assert.True(result);
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == "https://oauth2.googleapis.com/revoke"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task RevokeTokenAsync_ShouldReturnFalse_WhenRevocationFails()
    {
        // Arrange
        var refreshToken = "invalid-refresh-token";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == "https://oauth2.googleapis.com/revoke"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest
            });

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var service = new GoogleAuthenticationService(
            _mockConfiguration.Object,
            _mockUserService.Object,
            _mockHttpClientFactory.Object);

        // Act
        var result = await service.RevokeTokenAsync(refreshToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RevokeTokenAsync_ShouldReturnFalse_WhenExceptionOccurs()
    {
        // Arrange
        var refreshToken = "test-token";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var service = new GoogleAuthenticationService(
            _mockConfiguration.Object,
            _mockUserService.Object,
            _mockHttpClientFactory.Object);

        // Act
        var result = await service.RevokeTokenAsync(refreshToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GenerateJwtToken_ShouldReturnValidToken()
    {
        // Arrange
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var service = new GoogleAuthenticationService(
            _mockConfiguration.Object,
            _mockUserService.Object,
            _mockHttpClientFactory.Object);

        var userId = Guid.NewGuid().ToString();
        var email = "test@example.com";

        // Act
        var token = service.GenerateJwtToken(userId, email);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
        Assert.Contains(".", token); // JWT tokens have dots separating header, payload, and signature
    }
}

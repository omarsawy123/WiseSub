using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;
using WiseSub.Infrastructure.Email;
using WiseSub.Infrastructure.Security;
using Xunit;

namespace WiseSub.Infrastructure.Tests.Email;

public class GmailClientTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<GmailClient>> _mockLogger;
    private readonly Mock<IEmailAccountRepository> _mockEmailAccountRepository;
    private readonly Mock<ITokenEncryptionService> _mockTokenEncryptionService;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public GmailClientTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<GmailClient>>();
        _mockEmailAccountRepository = new Mock<IEmailAccountRepository>();
        _mockTokenEncryptionService = new Mock<ITokenEncryptionService>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();

        // Setup default configuration values
        _mockConfiguration.Setup(c => c["Authentication:Google:ClientId"]).Returns("test-client-id");
        _mockConfiguration.Setup(c => c["Authentication:Google:ClientSecret"]).Returns("test-client-secret");
        _mockConfiguration.Setup(c => c["Authentication:Google:RedirectUri"]).Returns("http://localhost/callback");
    }

    [Fact]
    public void GmailClient_Constructor_InitializesSuccessfully()
    {
        // Act
        var client = new GmailClient(
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockEmailAccountRepository.Object,
            _mockTokenEncryptionService.Object,
            _mockHttpClientFactory.Object);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public async Task RevokeAccessAsync_ValidAccountId_CallsRepositoryRevokeAccess()
    {
        // Arrange
        var accountId = "test-account-id";
        var emailAccount = new EmailAccount
        {
            Id = accountId,
            UserId = "test-user-id",
            EmailAddress = "test@gmail.com",
            Provider = EmailProvider.Gmail,
            EncryptedAccessToken = "encrypted-access-token",
            EncryptedRefreshToken = "encrypted-refresh-token",
            IsActive = true
        };

        _mockEmailAccountRepository
            .Setup(r => r.GetByIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emailAccount);

        _mockTokenEncryptionService
            .Setup(s => s.Decrypt("encrypted-refresh-token"))
            .Returns("refresh-token");

        _mockEmailAccountRepository
            .Setup(r => r.RevokeAccessAsync(accountId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var httpClient = new HttpClient(new MockHttpMessageHandler());
        _mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var client = new GmailClient(
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockEmailAccountRepository.Object,
            _mockTokenEncryptionService.Object,
            _mockHttpClientFactory.Object);

        // Act
        var result = await client.RevokeAccessAsync(accountId);

        // Assert
        Assert.True(result);
        _mockEmailAccountRepository.Verify(
            r => r.RevokeAccessAsync(accountId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeAccessAsync_NonExistentAccount_ReturnsFalse()
    {
        // Arrange
        var accountId = "non-existent-account";

        _mockEmailAccountRepository
            .Setup(r => r.GetByIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailAccount?)null);

        var client = new GmailClient(
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockEmailAccountRepository.Object,
            _mockTokenEncryptionService.Object,
            _mockHttpClientFactory.Object);

        // Act
        var result = await client.RevokeAccessAsync(accountId);

        // Assert
        Assert.False(result);
        _mockEmailAccountRepository.Verify(
            r => r.RevokeAccessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Mock HTTP message handler for testing
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}

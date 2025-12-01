using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Net;
using System.Net.Http.Headers;
using WiseSub.Application.Common.Configuration;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;
using WiseSub.Infrastructure.Email;
using Xunit;

namespace WiseSub.Infrastructure.Tests.Email;

public class EmailNotificationServiceTests
{
    private readonly Mock<ISendGridClient> _mockSendGridClient;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ISubscriptionRepository> _mockSubscriptionRepository;
    private readonly Mock<ILogger<EmailNotificationService>> _mockLogger;
    private readonly EmailNotificationConfiguration _config;
    private readonly EmailNotificationService _service;

    public EmailNotificationServiceTests()
    {
        _mockSendGridClient = new Mock<ISendGridClient>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockSubscriptionRepository = new Mock<ISubscriptionRepository>();
        _mockLogger = new Mock<ILogger<EmailNotificationService>>();
        
        _config = new EmailNotificationConfiguration
        {
            ApiKey = "test-api-key",
            SenderEmail = "test@wisesub.app",
            SenderName = "WiseSub Test",
            MaxRetries = 3,
            InitialRetryDelayMs = 10, // Short delay for tests
            Enabled = true,
            SandboxMode = false,
            ApplicationBaseUrl = "https://test.wisesub.app",
            MaxBatchSize = 10
        };

        var options = Options.Create(_config);

        _service = new EmailNotificationService(
            _mockSendGridClient.Object,
            options,
            _mockUserRepository.Object,
            _mockSubscriptionRepository.Object,
            _mockLogger.Object);
    }
    
    /// <summary>
    /// Creates a mock SendGrid response with specified status code
    /// </summary>
    private static Response CreateMockResponse(HttpStatusCode statusCode, string? messageId = null)
    {
        var httpResponseMessage = new HttpResponseMessage(statusCode);
        if (messageId != null)
        {
            httpResponseMessage.Headers.Add("X-Message-Id", messageId);
        }
        return new Response(statusCode, httpResponseMessage.Content, httpResponseMessage.Headers);
    }

    #region SendAlertAsync Tests

    [Fact]
    public async Task SendAlertAsync_WithNullAlert_ReturnsFailure()
    {
        // Act
        var result = await _service.SendAlertAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(EmailNotificationErrors.InvalidAlert.Code);
    }

    [Fact]
    public async Task SendAlertAsync_WhenDisabled_ReturnsSuccessWithoutSending()
    {
        // Arrange
        var config = new EmailNotificationConfiguration
        {
            Enabled = false,
            ApiKey = "test",
            SenderEmail = "test@test.com"
        };
        var service = new EmailNotificationService(
            _mockSendGridClient.Object,
            Options.Create(config),
            _mockUserRepository.Object,
            _mockSubscriptionRepository.Object,
            _mockLogger.Object);

        var alert = CreateTestAlert();

        // Act
        var result = await service.SendAlertAsync(alert);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.MessageId.Should().StartWith("disabled-");
        _mockSendGridClient.Verify(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAlertAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        var alert = CreateTestAlert();
        _mockUserRepository.Setup(x => x.GetByIdAsync(alert.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.SendAlertAsync(alert);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(UserErrors.NotFound.Code);
    }

    [Fact]
    public async Task SendAlertAsync_WithValidAlert_SendsEmailSuccessfully()
    {
        // Arrange
        var alert = CreateTestAlert();
        var user = CreateTestUser();
        var subscription = CreateTestSubscription();

        _mockUserRepository.Setup(x => x.GetByIdAsync(alert.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockSubscriptionRepository.Setup(x => x.GetByIdAsync(alert.SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var response = CreateMockResponse(HttpStatusCode.OK, "test-message-id");
        _mockSendGridClient.Setup(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _service.SendAlertAsync(alert);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Success.Should().BeTrue();
        result.Value.Status.Should().Be(DeliveryStatus.Sent);
        _mockSendGridClient.Verify(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(AlertType.RenewalUpcoming7Days, "renews in 7 days")]
    [InlineData(AlertType.RenewalUpcoming3Days, "renews in 3 days")]
    [InlineData(AlertType.PriceIncrease, "Price increase")]
    [InlineData(AlertType.TrialEnding, "trial is ending")]
    [InlineData(AlertType.UnusedSubscription, "haven't used")]
    public async Task SendAlertAsync_WithDifferentAlertTypes_SetsCorrectSubject(
        AlertType alertType, string expectedSubjectPart)
    {
        // Arrange
        var alert = CreateTestAlert(alertType);
        var user = CreateTestUser();
        var subscription = CreateTestSubscription();

        _mockUserRepository.Setup(x => x.GetByIdAsync(alert.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockSubscriptionRepository.Setup(x => x.GetByIdAsync(alert.SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SendGridMessage? capturedMessage = null;
        var response = CreateMockResponse(HttpStatusCode.OK);
        _mockSendGridClient.Setup(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()))
            .Callback<SendGridMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .ReturnsAsync(response);

        // Act
        await _service.SendAlertAsync(alert);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Subject.Should().Contain(expectedSubjectPart);
    }

    [Fact]
    public async Task SendAlertAsync_WhenSendGridFails_RetriesAndEventuallyFails()
    {
        // Arrange
        var alert = CreateTestAlert();
        var user = CreateTestUser();

        _mockUserRepository.Setup(x => x.GetByIdAsync(alert.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockSubscriptionRepository.Setup(x => x.GetByIdAsync(alert.SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        var response = CreateMockResponse(HttpStatusCode.InternalServerError);
        _mockSendGridClient.Setup(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _service.SendAlertAsync(alert);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(EmailNotificationErrors.SendFailed.Code);
        _mockSendGridClient.Verify(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()), Times.Exactly(4)); // Initial + 3 retries
    }

    [Fact]
    public async Task SendAlertAsync_WithClientError_DoesNotRetry()
    {
        // Arrange
        var alert = CreateTestAlert();
        var user = CreateTestUser();

        _mockUserRepository.Setup(x => x.GetByIdAsync(alert.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockSubscriptionRepository.Setup(x => x.GetByIdAsync(alert.SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        var response = CreateMockResponse(HttpStatusCode.BadRequest);
        _mockSendGridClient.Setup(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _service.SendAlertAsync(alert);

        // Assert
        result.IsFailure.Should().BeTrue();
        // Client errors (except 429) should not retry - only one call
        _mockSendGridClient.Verify(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region SendBatchAlertsAsync Tests

    [Fact]
    public async Task SendBatchAlertsAsync_WithNullAlerts_ReturnsFailure()
    {
        // Act
        var result = await _service.SendBatchAlertsAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(EmailNotificationErrors.InvalidAlert.Code);
    }

    [Fact]
    public async Task SendBatchAlertsAsync_WithEmptyAlerts_ReturnsSuccessWithZeroCounts()
    {
        // Act
        var result = await _service.SendBatchAlertsAsync(new List<Alert>());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalAttempted.Should().Be(0);
        result.Value.SuccessCount.Should().Be(0);
        result.Value.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task SendBatchAlertsAsync_WithMultipleAlerts_SendsAllAndReturnsResults()
    {
        // Arrange
        var alerts = new List<Alert>
        {
            CreateTestAlert(AlertType.RenewalUpcoming7Days),
            CreateTestAlert(AlertType.PriceIncrease),
            CreateTestAlert(AlertType.TrialEnding)
        };
        var user = CreateTestUser();

        _mockUserRepository.Setup(x => x.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockSubscriptionRepository.Setup(x => x.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        var response = CreateMockResponse(HttpStatusCode.OK);
        _mockSendGridClient.Setup(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _service.SendBatchAlertsAsync(alerts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalAttempted.Should().Be(3);
        result.Value.SuccessCount.Should().Be(3);
        result.Value.FailureCount.Should().Be(0);
        result.Value.AllSucceeded.Should().BeTrue();
        result.Value.SuccessRate.Should().Be(100);
    }

    [Fact]
    public async Task SendBatchAlertsAsync_WithPartialFailures_ReturnsCorrectCounts()
    {
        // Arrange
        var alerts = new List<Alert>
        {
            CreateTestAlert(),
            CreateTestAlert(),
        };
        
        var user1 = CreateTestUser();
        _mockUserRepository.SetupSequence(x => x.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user1)
            .ReturnsAsync((User?)null); // Second one fails

        var response = CreateMockResponse(HttpStatusCode.OK);
        _mockSendGridClient.Setup(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _service.SendBatchAlertsAsync(alerts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalAttempted.Should().Be(2);
        result.Value.SuccessCount.Should().Be(1);
        result.Value.FailureCount.Should().Be(1);
        result.Value.AllSucceeded.Should().BeFalse();
        result.Value.SuccessRate.Should().Be(50);
    }

    #endregion

    #region SendDailyDigestAsync Tests

    [Fact]
    public async Task SendDailyDigestAsync_WithNullUserId_ReturnsFailure()
    {
        // Act
        var result = await _service.SendDailyDigestAsync(null!, new List<Alert>());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(UserErrors.NotFound.Code);
    }

    [Fact]
    public async Task SendDailyDigestAsync_WithNullAlerts_ReturnsFailure()
    {
        // Act
        var result = await _service.SendDailyDigestAsync("user-123", null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(EmailNotificationErrors.InvalidAlert.Code);
    }

    [Fact]
    public async Task SendDailyDigestAsync_WithEmptyAlerts_ReturnsSuccessWithoutSending()
    {
        // Act
        var result = await _service.SendDailyDigestAsync("user-123", new List<Alert>());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.MessageId.Should().StartWith("no-alerts-");
        _mockSendGridClient.Verify(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendDailyDigestAsync_WhenDisabled_ReturnsSuccessWithoutSending()
    {
        // Arrange
        var config = new EmailNotificationConfiguration
        {
            Enabled = false,
            ApiKey = "test",
            SenderEmail = "test@test.com"
        };
        var service = new EmailNotificationService(
            _mockSendGridClient.Object,
            Options.Create(config),
            _mockUserRepository.Object,
            _mockSubscriptionRepository.Object,
            _mockLogger.Object);

        var alerts = new List<Alert> { CreateTestAlert() };

        // Act
        var result = await service.SendDailyDigestAsync("user-123", alerts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.MessageId.Should().StartWith("disabled-digest-");
        _mockSendGridClient.Verify(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendDailyDigestAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        var alerts = new List<Alert> { CreateTestAlert() };
        _mockUserRepository.Setup(x => x.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.SendDailyDigestAsync("user-123", alerts);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(UserErrors.NotFound.Code);
    }

    [Fact]
    public async Task SendDailyDigestAsync_WithValidData_SendsDigestEmail()
    {
        // Arrange
        var userId = "user-123";
        var user = CreateTestUser();
        var alerts = new List<Alert>
        {
            CreateTestAlert(AlertType.RenewalUpcoming7Days),
            CreateTestAlert(AlertType.PriceIncrease)
        };
        var subscription = CreateTestSubscription();

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockSubscriptionRepository.Setup(x => x.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SendGridMessage? capturedMessage = null;
        var response = CreateMockResponse(HttpStatusCode.OK);
        _mockSendGridClient.Setup(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()))
            .Callback<SendGridMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .ReturnsAsync(response);

        // Act
        var result = await _service.SendDailyDigestAsync(userId, alerts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Success.Should().BeTrue();
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Subject.Should().Contain("Daily Digest");
    }

    #endregion

    #region GetDeliveryStatusAsync Tests

    [Fact]
    public async Task GetDeliveryStatusAsync_WithNullMessageId_ReturnsFailure()
    {
        // Act
        var result = await _service.GetDeliveryStatusAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(EmailNotificationErrors.InvalidMessageId.Code);
    }

    [Fact]
    public async Task GetDeliveryStatusAsync_WithEmptyMessageId_ReturnsFailure()
    {
        // Act
        var result = await _service.GetDeliveryStatusAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(EmailNotificationErrors.InvalidMessageId.Code);
    }

    [Fact]
    public async Task GetDeliveryStatusAsync_WithValidMessageId_ReturnsUnknown()
    {
        // Act
        var result = await _service.GetDeliveryStatusAsync("test-message-id");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DeliveryStatus.Unknown);
    }

    #endregion

    #region ValidateConfigurationAsync Tests

    [Fact]
    public async Task ValidateConfigurationAsync_WithMissingApiKey_ReturnsFailure()
    {
        // Arrange
        var config = new EmailNotificationConfiguration
        {
            ApiKey = "",
            SenderEmail = "test@test.com"
        };
        var service = new EmailNotificationService(
            _mockSendGridClient.Object,
            Options.Create(config),
            _mockUserRepository.Object,
            _mockSubscriptionRepository.Object,
            _mockLogger.Object);

        // Act
        var result = await service.ValidateConfigurationAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(EmailNotificationErrors.InvalidConfiguration.Code);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithPlaceholderApiKey_ReturnsFailure()
    {
        // Arrange
        var config = new EmailNotificationConfiguration
        {
            ApiKey = "YOUR_SENDGRID_API_KEY",
            SenderEmail = "test@test.com"
        };
        var service = new EmailNotificationService(
            _mockSendGridClient.Object,
            Options.Create(config),
            _mockUserRepository.Object,
            _mockSubscriptionRepository.Object,
            _mockLogger.Object);

        // Act
        var result = await service.ValidateConfigurationAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(EmailNotificationErrors.InvalidConfiguration.Code);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithMissingSenderEmail_ReturnsFailure()
    {
        // Arrange
        var config = new EmailNotificationConfiguration
        {
            ApiKey = "valid-api-key",
            SenderEmail = ""
        };
        var service = new EmailNotificationService(
            _mockSendGridClient.Object,
            Options.Create(config),
            _mockUserRepository.Object,
            _mockSubscriptionRepository.Object,
            _mockLogger.Object);

        // Act
        var result = await service.ValidateConfigurationAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(EmailNotificationErrors.InvalidConfiguration.Code);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithValidConfig_ReturnsSuccess()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateConfigurationAsync_InSandboxMode_SendsTestEmail()
    {
        // Arrange
        var config = new EmailNotificationConfiguration
        {
            ApiKey = "valid-api-key",
            SenderEmail = "test@test.com",
            SandboxMode = true
        };
        var service = new EmailNotificationService(
            _mockSendGridClient.Object,
            Options.Create(config),
            _mockUserRepository.Object,
            _mockSubscriptionRepository.Object,
            _mockLogger.Object);

        var response = CreateMockResponse(HttpStatusCode.OK);
        _mockSendGridClient.Setup(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await service.ValidateConfigurationAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockSendGridClient.Verify(x => x.SendEmailAsync(
            It.Is<SendGridMessage>(m => m.MailSettings != null && m.MailSettings.SandboxMode != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Email Content Tests

    [Fact]
    public async Task SendAlertAsync_WithPriceChangeAlert_IncludesPriceInfo()
    {
        // Arrange
        var alert = CreateTestAlert(AlertType.PriceIncrease);
        alert.Message = "Price increased from $9.99 to $14.99";
        var user = CreateTestUser();
        var subscription = CreateTestSubscription();
        subscription.Price = 14.99m;

        _mockUserRepository.Setup(x => x.GetByIdAsync(alert.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockSubscriptionRepository.Setup(x => x.GetByIdAsync(alert.SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SendGridMessage? capturedMessage = null;
        var response = CreateMockResponse(HttpStatusCode.OK);
        _mockSendGridClient.Setup(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()))
            .Callback<SendGridMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .ReturnsAsync(response);

        // Act
        await _service.SendAlertAsync(alert);

        // Assert
        capturedMessage.Should().NotBeNull();
        // The HTML content should contain price information
        capturedMessage!.HtmlContent.Should().Contain("$14.99");
    }

    [Fact]
    public async Task SendAlertAsync_WithCancellationLink_IncludesLinkInEmail()
    {
        // Arrange
        var alert = CreateTestAlert();
        var user = CreateTestUser();
        var subscription = CreateTestSubscription();
        subscription.CancellationLink = "https://example.com/cancel";

        _mockUserRepository.Setup(x => x.GetByIdAsync(alert.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockSubscriptionRepository.Setup(x => x.GetByIdAsync(alert.SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SendGridMessage? capturedMessage = null;
        var response = CreateMockResponse(HttpStatusCode.OK);
        _mockSendGridClient.Setup(x => x.SendEmailAsync(
            It.IsAny<SendGridMessage>(),
            It.IsAny<CancellationToken>()))
            .Callback<SendGridMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .ReturnsAsync(response);

        // Act
        await _service.SendAlertAsync(alert);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.HtmlContent.Should().Contain("https://example.com/cancel");
    }

    #endregion

    #region Helper Methods

    private static Alert CreateTestAlert(AlertType type = AlertType.RenewalUpcoming7Days)
    {
        return new Alert
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "user-123",
            SubscriptionId = "sub-123",
            Type = type,
            Message = "Test alert message",
            ScheduledFor = DateTime.UtcNow,
            Status = AlertStatus.Pending
        };
    }

    private static User CreateTestUser()
    {
        return new User
        {
            Id = "user-123",
            Email = "test@example.com",
            Name = "Test User"
        };
    }

    private static Subscription CreateTestSubscription()
    {
        return new Subscription
        {
            Id = "sub-123",
            UserId = "user-123",
            ServiceName = "Netflix",
            Price = 15.99m,
            Currency = "USD",
            BillingCycle = BillingCycle.Monthly,
            NextRenewalDate = DateTime.UtcNow.AddDays(7),
            Status = SubscriptionStatus.Active
        };
    }

    #endregion
}

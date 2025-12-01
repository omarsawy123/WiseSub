using FluentAssertions;
using Moq;
using System.Text.Json;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Services;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Tests.Services;

/// <summary>
/// Unit tests for AlertService
/// Covers Task 11 - Alert Service implementation
/// </summary>
public class AlertServiceTests
{
    private readonly Mock<IAlertRepository> _mockAlertRepository;
    private readonly Mock<ISubscriptionRepository> _mockSubscriptionRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly AlertService _service;

    public AlertServiceTests()
    {
        _mockAlertRepository = new Mock<IAlertRepository>();
        _mockSubscriptionRepository = new Mock<ISubscriptionRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _service = new AlertService(
            _mockAlertRepository.Object,
            _mockSubscriptionRepository.Object,
            _mockUserRepository.Object);
    }

    #region GenerateRenewalAlertsAsync Tests

    [Fact]
    public async Task GenerateRenewalAlertsAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.GenerateRenewalAlertsAsync("user-123");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(UserErrors.NotFound.Code);
    }

    [Fact]
    public async Task GenerateRenewalAlertsAsync_RenewalAlertsDisabled_ReturnsEmpty()
    {
        // Arrange
        var user = CreateUserWithPreferences(enableRenewalAlerts: false);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.GenerateRenewalAlertsAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateRenewalAlertsAsync_SubscriptionRenewingIn7Days_Creates7DayAlert()
    {
        // Arrange
        var user = CreateUserWithPreferences(enableRenewalAlerts: true);
        var subscription = new Subscription
        {
            Id = "sub-123",
            UserId = "user-123",
            ServiceName = "Netflix",
            NextRenewalDate = DateTime.UtcNow.AddDays(7).Date,
            Status = SubscriptionStatus.Active,
            Price = 15.99m,
            Currency = "USD"
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAndStatusAsync("user-123", SubscriptionStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { subscription });

        _mockAlertRepository
            .Setup(r => r.GetBySubscriptionAndTypeAsync("sub-123", AlertType.RenewalUpcoming7Days, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alert?)null);

        _mockAlertRepository
            .Setup(r => r.GetBySubscriptionAndTypeAsync("sub-123", AlertType.RenewalUpcoming3Days, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alert?)null);

        Alert? capturedAlert = null;
        _mockAlertRepository
            .Setup(r => r.AddAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .Callback<Alert, CancellationToken>((a, _) => capturedAlert = a)
            .ReturnsAsync((Alert a, CancellationToken _) => a);

        // Act
        var result = await _service.GenerateRenewalAlertsAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Type.Should().Be(AlertType.RenewalUpcoming7Days);
        capturedAlert.Message.Should().Contain("7 days");
        capturedAlert.Message.Should().Contain("Netflix");
    }

    [Fact]
    public async Task GenerateRenewalAlertsAsync_SubscriptionRenewingIn3Days_Creates3DayAlert()
    {
        // Arrange
        var user = CreateUserWithPreferences(enableRenewalAlerts: true);
        var subscription = new Subscription
        {
            Id = "sub-123",
            UserId = "user-123",
            ServiceName = "Spotify",
            NextRenewalDate = DateTime.UtcNow.AddDays(3).Date,
            Status = SubscriptionStatus.Active,
            Price = 9.99m,
            Currency = "USD"
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAndStatusAsync("user-123", SubscriptionStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { subscription });

        _mockAlertRepository
            .Setup(r => r.GetBySubscriptionAndTypeAsync("sub-123", It.IsAny<AlertType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alert?)null);

        var capturedAlerts = new List<Alert>();
        _mockAlertRepository
            .Setup(r => r.AddAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .Callback<Alert, CancellationToken>((a, _) => capturedAlerts.Add(a))
            .ReturnsAsync((Alert a, CancellationToken _) => a);

        // Act
        var result = await _service.GenerateRenewalAlertsAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedAlerts.Should().Contain(a => a.Type == AlertType.RenewalUpcoming3Days);
    }

    [Fact]
    public async Task GenerateRenewalAlertsAsync_ExistingPendingAlert_SkipsDuplicate()
    {
        // Arrange
        var user = CreateUserWithPreferences(enableRenewalAlerts: true);
        var subscription = new Subscription
        {
            Id = "sub-123",
            UserId = "user-123",
            ServiceName = "Netflix",
            NextRenewalDate = DateTime.UtcNow.AddDays(7).Date,
            Status = SubscriptionStatus.Active
        };

        var existingAlert = new Alert
        {
            Id = "alert-123",
            SubscriptionId = "sub-123",
            Type = AlertType.RenewalUpcoming7Days,
            Status = AlertStatus.Pending
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAndStatusAsync("user-123", SubscriptionStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { subscription });

        _mockAlertRepository
            .Setup(r => r.GetBySubscriptionAndTypeAsync("sub-123", AlertType.RenewalUpcoming7Days, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAlert);

        _mockAlertRepository
            .Setup(r => r.GetBySubscriptionAndTypeAsync("sub-123", AlertType.RenewalUpcoming3Days, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alert?)null);

        // Act
        var result = await _service.GenerateRenewalAlertsAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockAlertRepository.Verify(
            r => r.AddAsync(It.Is<Alert>(a => a.Type == AlertType.RenewalUpcoming7Days), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateRenewalAlertsAsync_CancelledSubscription_NoAlertCreated()
    {
        // Arrange
        var user = CreateUserWithPreferences(enableRenewalAlerts: true);

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // The service only queries for Active subscriptions, so cancelled ones won't be returned
        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAndStatusAsync("user-123", SubscriptionStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Subscription>());

        // Act
        var result = await _service.GenerateRenewalAlertsAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    #endregion

    #region GeneratePriceChangeAlertsAsync Tests

    [Fact]
    public async Task GeneratePriceChangeAlertsAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.GeneratePriceChangeAlertsAsync("user-123");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(UserErrors.NotFound.Code);
    }

    [Fact]
    public async Task GeneratePriceChangeAlertsAsync_PriceChangeAlertsDisabled_ReturnsEmpty()
    {
        // Arrange
        var user = CreateUserWithPreferences(enablePriceChangeAlerts: false);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.GeneratePriceChangeAlertsAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GeneratePriceChangeAlertsAsync_PriceIncrease_CreatesAlert()
    {
        // Arrange
        var user = CreateUserWithPreferences(enablePriceChangeAlerts: true);
        var subscription = new Subscription
        {
            Id = "sub-123",
            UserId = "user-123",
            ServiceName = "Netflix",
            Price = 18.99m,
            Currency = "USD",
            Status = SubscriptionStatus.Active,
            History = new List<SubscriptionHistory>
            {
                new SubscriptionHistory
                {
                    Id = "hist-1",
                    SubscriptionId = "sub-123",
                    ChangeType = "Price",
                    OldValue = "15.99",
                    NewValue = "18.99",
                    ChangedAt = DateTime.UtcNow.AddDays(-1)
                }
            }
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { subscription });

        _mockAlertRepository
            .Setup(r => r.GetBySubscriptionAndTypeAsync("sub-123", AlertType.PriceIncrease, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alert?)null);

        Alert? capturedAlert = null;
        _mockAlertRepository
            .Setup(r => r.AddAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .Callback<Alert, CancellationToken>((a, _) => capturedAlert = a)
            .ReturnsAsync((Alert a, CancellationToken _) => a);

        // Act
        var result = await _service.GeneratePriceChangeAlertsAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Type.Should().Be(AlertType.PriceIncrease);
        capturedAlert.Message.Should().Contain("Netflix");
        capturedAlert.Message.Should().Contain("15.99");
        capturedAlert.Message.Should().Contain("18.99");
    }

    [Fact]
    public async Task GeneratePriceChangeAlertsAsync_PriceDecrease_NoAlertCreated()
    {
        // Arrange
        var user = CreateUserWithPreferences(enablePriceChangeAlerts: true);
        var subscription = new Subscription
        {
            Id = "sub-123",
            UserId = "user-123",
            ServiceName = "Netflix",
            Price = 12.99m,
            Currency = "USD",
            Status = SubscriptionStatus.Active,
            History = new List<SubscriptionHistory>
            {
                new SubscriptionHistory
                {
                    Id = "hist-1",
                    SubscriptionId = "sub-123",
                    ChangeType = "Price",
                    OldValue = "15.99",
                    NewValue = "12.99",
                    ChangedAt = DateTime.UtcNow.AddDays(-1)
                }
            }
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { subscription });

        // Act
        var result = await _service.GeneratePriceChangeAlertsAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    #endregion

    #region GenerateTrialEndingAlertsAsync Tests

    [Fact]
    public async Task GenerateTrialEndingAlertsAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.GenerateTrialEndingAlertsAsync("user-123");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(UserErrors.NotFound.Code);
    }

    [Fact]
    public async Task GenerateTrialEndingAlertsAsync_TrialEndingIn3Days_CreatesAlert()
    {
        // Arrange
        var user = CreateUserWithPreferences(enableTrialEndingAlerts: true);
        var subscription = new Subscription
        {
            Id = "sub-123",
            UserId = "user-123",
            ServiceName = "Disney+",
            NextRenewalDate = DateTime.UtcNow.AddDays(3).Date,
            Status = SubscriptionStatus.TrialActive
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAndStatusAsync("user-123", SubscriptionStatus.TrialActive, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { subscription });

        _mockAlertRepository
            .Setup(r => r.GetBySubscriptionAndTypeAsync("sub-123", AlertType.TrialEnding, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alert?)null);

        Alert? capturedAlert = null;
        _mockAlertRepository
            .Setup(r => r.AddAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .Callback<Alert, CancellationToken>((a, _) => capturedAlert = a)
            .ReturnsAsync((Alert a, CancellationToken _) => a);

        // Act
        var result = await _service.GenerateTrialEndingAlertsAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Type.Should().Be(AlertType.TrialEnding);
        capturedAlert.Message.Should().Contain("Disney+");
        capturedAlert.Message.Should().Contain("3 days");
    }

    [Fact]
    public async Task GenerateTrialEndingAlertsAsync_TrialAlertsDisabled_ReturnsEmpty()
    {
        // Arrange
        var user = CreateUserWithPreferences(enableTrialEndingAlerts: false);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.GenerateTrialEndingAlertsAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    #endregion

    #region GenerateUnusedSubscriptionAlertsAsync Tests

    [Fact]
    public async Task GenerateUnusedSubscriptionAlertsAsync_NoActivityFor6Months_CreatesAlert()
    {
        // Arrange
        var user = CreateUserWithPreferences(enableUnusedSubscriptionAlerts: true);
        var subscription = new Subscription
        {
            Id = "sub-123",
            UserId = "user-123",
            ServiceName = "HBO Max",
            Status = SubscriptionStatus.Active,
            Price = 14.99m,
            Currency = "USD",
            BillingCycle = BillingCycle.Monthly,
            LastActivityEmailAt = DateTime.UtcNow.AddMonths(-7)
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAndStatusAsync("user-123", SubscriptionStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { subscription });

        _mockAlertRepository
            .Setup(r => r.GetBySubscriptionAndTypeAsync("sub-123", AlertType.UnusedSubscription, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alert?)null);

        Alert? capturedAlert = null;
        _mockAlertRepository
            .Setup(r => r.AddAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .Callback<Alert, CancellationToken>((a, _) => capturedAlert = a)
            .ReturnsAsync((Alert a, CancellationToken _) => a);

        // Act
        var result = await _service.GenerateUnusedSubscriptionAlertsAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Type.Should().Be(AlertType.UnusedSubscription);
        capturedAlert.Message.Should().Contain("HBO Max");
        capturedAlert.Message.Should().Contain("months"); // Could be 6 or 7 depending on exact timing
    }

    [Fact]
    public async Task GenerateUnusedSubscriptionAlertsAsync_RecentActivity_NoAlertCreated()
    {
        // Arrange
        var user = CreateUserWithPreferences(enableUnusedSubscriptionAlerts: true);
        var subscription = new Subscription
        {
            Id = "sub-123",
            UserId = "user-123",
            ServiceName = "HBO Max",
            Status = SubscriptionStatus.Active,
            LastActivityEmailAt = DateTime.UtcNow.AddDays(-30) // Recent activity
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAndStatusAsync("user-123", SubscriptionStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { subscription });

        // Act
        var result = await _service.GenerateUnusedSubscriptionAlertsAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateUnusedSubscriptionAlertsAsync_NullLastActivity_NoAlertCreated()
    {
        // Arrange
        var user = CreateUserWithPreferences(enableUnusedSubscriptionAlerts: true);
        var subscription = new Subscription
        {
            Id = "sub-123",
            UserId = "user-123",
            ServiceName = "HBO Max",
            Status = SubscriptionStatus.Active,
            LastActivityEmailAt = null,
            CreatedAt = DateTime.UtcNow.AddMonths(-3) // Created 3 months ago, so not unused
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAndStatusAsync("user-123", SubscriptionStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { subscription });

        // Act
        var result = await _service.GenerateUnusedSubscriptionAlertsAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    #endregion

    #region GenerateAllAlertsAsync Tests

    [Fact]
    public async Task GenerateAllAlertsAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.GenerateAllAlertsAsync("user-123");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(UserErrors.NotFound.Code);
    }

    [Fact]
    public async Task GenerateAllAlertsAsync_AllAlertsEnabled_ReturnsSummary()
    {
        // Arrange
        var user = CreateUserWithPreferences(
            enableRenewalAlerts: true,
            enablePriceChangeAlerts: true,
            enableTrialEndingAlerts: true,
            enableUnusedSubscriptionAlerts: true);

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Subscription>());

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAndStatusAsync("user-123", SubscriptionStatus.TrialActive, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Subscription>());

        // Act
        var result = await _service.GenerateAllAlertsAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TotalAlertsGenerated.Should().Be(0);
    }

    #endregion

    #region MarkAlertAsSentAsync Tests

    [Fact]
    public async Task MarkAlertAsSentAsync_AlertNotFound_ReturnsFailure()
    {
        // Arrange
        _mockAlertRepository
            .Setup(r => r.GetByIdAsync("alert-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alert?)null);

        // Act
        var result = await _service.MarkAlertAsSentAsync("alert-123");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(AlertErrors.NotFound.Code);
    }

    [Fact]
    public async Task MarkAlertAsSentAsync_ValidAlert_CallsRepository()
    {
        // Arrange
        var alert = new Alert { Id = "alert-123", Status = AlertStatus.Pending };
        _mockAlertRepository
            .Setup(r => r.GetByIdAsync("alert-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        // Act
        var result = await _service.MarkAlertAsSentAsync("alert-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockAlertRepository.Verify(r => r.MarkAsSentAsync("alert-123", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region MarkAlertAsFailedAsync Tests

    [Fact]
    public async Task MarkAlertAsFailedAsync_AlertNotFound_ReturnsFailure()
    {
        // Arrange
        _mockAlertRepository
            .Setup(r => r.GetByIdAsync("alert-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alert?)null);

        // Act
        var result = await _service.MarkAlertAsFailedAsync("alert-123");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(AlertErrors.NotFound.Code);
    }

    [Fact]
    public async Task MarkAlertAsFailedAsync_MaxRetriesExceeded_MarksAsFailed()
    {
        // Arrange
        var alert = new Alert { Id = "alert-123", Status = AlertStatus.Pending, RetryCount = 2 };
        _mockAlertRepository
            .Setup(r => r.GetByIdAsync("alert-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        // Act
        var result = await _service.MarkAlertAsFailedAsync("alert-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockAlertRepository.Verify(r => r.MarkAsFailedAsync("alert-123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkAlertAsFailedAsync_RetriesRemaining_IncrementsRetryCount()
    {
        // Arrange
        var alert = new Alert { Id = "alert-123", Status = AlertStatus.Pending, RetryCount = 0 };
        _mockAlertRepository
            .Setup(r => r.GetByIdAsync("alert-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        // Act
        var result = await _service.MarkAlertAsFailedAsync("alert-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        alert.RetryCount.Should().Be(1);
        _mockAlertRepository.Verify(r => r.UpdateAsync(alert, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region SnoozeAlertAsync Tests

    [Fact]
    public async Task SnoozeAlertAsync_AlertNotFound_ReturnsFailure()
    {
        // Arrange
        _mockAlertRepository
            .Setup(r => r.GetByIdAsync("alert-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alert?)null);

        // Act
        var result = await _service.SnoozeAlertAsync("alert-123");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(AlertErrors.NotFound.Code);
    }

    [Fact]
    public async Task SnoozeAlertAsync_ValidAlert_UpdatesScheduledFor()
    {
        // Arrange
        var originalScheduledFor = DateTime.UtcNow;
        var alert = new Alert 
        { 
            Id = "alert-123", 
            Status = AlertStatus.Pending,
            ScheduledFor = originalScheduledFor
        };
        _mockAlertRepository
            .Setup(r => r.GetByIdAsync("alert-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        // Act
        var result = await _service.SnoozeAlertAsync("alert-123", snoozeHours: 12);

        // Assert
        result.IsSuccess.Should().BeTrue();
        alert.Status.Should().Be(AlertStatus.Snoozed);
        alert.ScheduledFor.Should().BeCloseTo(originalScheduledFor.AddHours(12), TimeSpan.FromMinutes(1));
        _mockAlertRepository.Verify(r => r.UpdateAsync(alert, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DismissAlertAsync Tests

    [Fact]
    public async Task DismissAlertAsync_AlertNotFound_ReturnsFailure()
    {
        // Arrange
        _mockAlertRepository
            .Setup(r => r.GetByIdAsync("alert-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alert?)null);

        // Act
        var result = await _service.DismissAlertAsync("alert-123");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(AlertErrors.NotFound.Code);
    }

    [Fact]
    public async Task DismissAlertAsync_ValidAlert_DeletesAlert()
    {
        // Arrange
        var alert = new Alert { Id = "alert-123", Status = AlertStatus.Pending };
        _mockAlertRepository
            .Setup(r => r.GetByIdAsync("alert-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        // Act
        var result = await _service.DismissAlertAsync("alert-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockAlertRepository.Verify(r => r.DeleteAsync(alert, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetUserPreferencesAsync Tests

    [Fact]
    public async Task GetUserPreferencesAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.GetUserPreferencesAsync("user-123");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(UserErrors.NotFound.Code);
    }

    [Fact]
    public async Task GetUserPreferencesAsync_ValidUser_ReturnsPreferences()
    {
        // Arrange
        var preferences = new UserPreferences
        {
            EnableRenewalAlerts = true,
            EnablePriceChangeAlerts = false
        };
        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            PreferencesJson = JsonSerializer.Serialize(preferences)
        };
        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.GetUserPreferencesAsync("user-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EnableRenewalAlerts.Should().BeTrue();
        result.Value.EnablePriceChangeAlerts.Should().BeFalse();
    }

    #endregion

    #region UpdateUserPreferencesAsync Tests

    [Fact]
    public async Task UpdateUserPreferencesAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.UpdateUserPreferencesAsync("user-123", new UserPreferences());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(UserErrors.NotFound.Code);
    }

    [Fact]
    public async Task UpdateUserPreferencesAsync_ValidUser_UpdatesPreferences()
    {
        // Arrange
        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            PreferencesJson = "{}"
        };
        var newPreferences = new UserPreferences
        {
            EnableRenewalAlerts = false,
            EnablePriceChangeAlerts = true
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.UpdateUserPreferencesAsync("user-123", newPreferences);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUserRepository.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        
        var savedPreferences = JsonSerializer.Deserialize<UserPreferences>(user.PreferencesJson);
        savedPreferences!.EnableRenewalAlerts.Should().BeFalse();
        savedPreferences.EnablePriceChangeAlerts.Should().BeTrue();
    }

    #endregion

    #region CreateAlertAsync Tests

    [Fact]
    public async Task CreateAlertAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var request = new CreateAlertRequest
        {
            UserId = "user-123",
            SubscriptionId = "sub-123",
            Type = AlertType.RenewalUpcoming7Days,
            Message = "Test alert",
            ScheduledFor = DateTime.UtcNow
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.CreateAlertAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(UserErrors.NotFound.Code);
    }

    [Fact]
    public async Task CreateAlertAsync_SubscriptionNotFound_ReturnsFailure()
    {
        // Arrange
        var user = CreateUserWithPreferences();
        var request = new CreateAlertRequest
        {
            UserId = "user-123",
            SubscriptionId = "sub-123",
            Type = AlertType.RenewalUpcoming7Days,
            Message = "Test alert",
            ScheduledFor = DateTime.UtcNow
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSubscriptionRepository
            .Setup(r => r.GetByIdAsync("sub-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        // Act
        var result = await _service.CreateAlertAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(SubscriptionErrors.NotFound.Code);
    }

    [Fact]
    public async Task CreateAlertAsync_DuplicatePendingAlert_ReturnsFailure()
    {
        // Arrange
        var user = CreateUserWithPreferences();
        var subscription = new Subscription { Id = "sub-123", UserId = "user-123" };
        var existingAlert = new Alert
        {
            Id = "existing-alert",
            SubscriptionId = "sub-123",
            Type = AlertType.RenewalUpcoming7Days,
            Status = AlertStatus.Pending
        };
        var request = new CreateAlertRequest
        {
            UserId = "user-123",
            SubscriptionId = "sub-123",
            Type = AlertType.RenewalUpcoming7Days,
            Message = "Test alert",
            ScheduledFor = DateTime.UtcNow
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSubscriptionRepository
            .Setup(r => r.GetByIdAsync("sub-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _mockAlertRepository
            .Setup(r => r.GetBySubscriptionAndTypeAsync("sub-123", AlertType.RenewalUpcoming7Days, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAlert);

        // Act
        var result = await _service.CreateAlertAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(AlertErrors.AlreadySent.Code);
    }

    [Fact]
    public async Task CreateAlertAsync_ValidRequest_CreatesAlert()
    {
        // Arrange
        var user = CreateUserWithPreferences();
        var subscription = new Subscription { Id = "sub-123", UserId = "user-123" };
        var request = new CreateAlertRequest
        {
            UserId = "user-123",
            SubscriptionId = "sub-123",
            Type = AlertType.RenewalUpcoming7Days,
            Message = "Test alert",
            ScheduledFor = DateTime.UtcNow.AddDays(1)
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockSubscriptionRepository
            .Setup(r => r.GetByIdAsync("sub-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _mockAlertRepository
            .Setup(r => r.GetBySubscriptionAndTypeAsync("sub-123", AlertType.RenewalUpcoming7Days, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alert?)null);

        Alert? capturedAlert = null;
        _mockAlertRepository
            .Setup(r => r.AddAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .Callback<Alert, CancellationToken>((a, _) => capturedAlert = a)
            .ReturnsAsync((Alert a, CancellationToken _) => a);

        // Act
        var result = await _service.CreateAlertAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedAlert.Should().NotBeNull();
        capturedAlert!.UserId.Should().Be("user-123");
        capturedAlert.SubscriptionId.Should().Be("sub-123");
        capturedAlert.Type.Should().Be(AlertType.RenewalUpcoming7Days);
        capturedAlert.Message.Should().Be("Test alert");
        capturedAlert.Status.Should().Be(AlertStatus.Pending);
    }

    #endregion

    #region GetUserAlertsAsync Tests

    [Fact]
    public async Task GetUserAlertsAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.GetUserAlertsAsync("user-123");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(UserErrors.NotFound.Code);
    }

    [Fact]
    public async Task GetUserAlertsAsync_WithStatusFilter_ReturnsFilteredAlerts()
    {
        // Arrange
        var user = CreateUserWithPreferences();
        var alerts = new List<Alert>
        {
            new Alert { Id = "alert-1", UserId = "user-123", Status = AlertStatus.Pending, ScheduledFor = DateTime.UtcNow },
            new Alert { Id = "alert-2", UserId = "user-123", Status = AlertStatus.Sent, ScheduledFor = DateTime.UtcNow.AddDays(-1) }
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockAlertRepository
            .Setup(r => r.GetByUserIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(alerts);

        // Act
        var result = await _service.GetUserAlertsAsync("user-123", statusFilter: AlertStatus.Pending);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value.First().Id.Should().Be("alert-1");
    }

    [Fact]
    public async Task GetUserAlertsAsync_WithTypeFilter_ReturnsFilteredAlerts()
    {
        // Arrange
        var user = CreateUserWithPreferences();
        var alerts = new List<Alert>
        {
            new Alert { Id = "alert-1", UserId = "user-123", Type = AlertType.RenewalUpcoming7Days, ScheduledFor = DateTime.UtcNow },
            new Alert { Id = "alert-2", UserId = "user-123", Type = AlertType.PriceIncrease, ScheduledFor = DateTime.UtcNow }
        };

        _mockUserRepository
            .Setup(r => r.GetByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockAlertRepository
            .Setup(r => r.GetByUserIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(alerts);

        // Act
        var result = await _service.GetUserAlertsAsync("user-123", typeFilter: AlertType.PriceIncrease);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value.First().Type.Should().Be(AlertType.PriceIncrease);
    }

    #endregion

    #region Helper Methods

    private static User CreateUserWithPreferences(
        bool enableRenewalAlerts = true,
        bool enablePriceChangeAlerts = true,
        bool enableTrialEndingAlerts = true,
        bool enableUnusedSubscriptionAlerts = true)
    {
        var preferences = new UserPreferences
        {
            EnableRenewalAlerts = enableRenewalAlerts,
            EnablePriceChangeAlerts = enablePriceChangeAlerts,
            EnableTrialEndingAlerts = enableTrialEndingAlerts,
            EnableUnusedSubscriptionAlerts = enableUnusedSubscriptionAlerts
        };

        return new User
        {
            Id = "user-123",
            Email = "test@example.com",
            Name = "Test User",
            PreferencesJson = JsonSerializer.Serialize(preferences)
        };
    }

    #endregion
}

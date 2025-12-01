using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Services;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Tests.Services;

/// <summary>
/// Property tests for TierService
/// Covers three-tier pricing model: Free, Pro, Premium
/// Task 14.1 (free tier limit enforcement), Task 14.2 (paid tier feature unlock),
/// Task 14.3 (downgrade data preservation), Task 14.4 (three-tier model)
/// </summary>
public class TierServiceTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IEmailAccountRepository> _mockEmailAccountRepository;
    private readonly Mock<ISubscriptionRepository> _mockSubscriptionRepository;
    private readonly Mock<ILogger<TierService>> _mockLogger;
    private readonly TierService _service;

    public TierServiceTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockEmailAccountRepository = new Mock<IEmailAccountRepository>();
        _mockSubscriptionRepository = new Mock<ISubscriptionRepository>();
        _mockLogger = new Mock<ILogger<TierService>>();
        
        _service = new TierService(
            _mockUserRepository.Object,
            _mockEmailAccountRepository.Object,
            _mockSubscriptionRepository.Object,
            _mockLogger.Object);
    }

    #region Task 14.1 - Property 57: Free Tier Limit Enforcement

    /// <summary>
    /// Feature: subscription-tracker, Property 57: Free tier limit enforcement
    /// For any free tier user with 5 subscriptions, attempts to add a 6th subscription SHALL be blocked
    /// Validates: Requirements 14.2
    /// </summary>
    [Fact]
    public async Task ValidateOperation_FreeTierAtSubscriptionLimit_ReturnsFailure()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Free };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockEmailAccountRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailAccount> { new() { IsActive = true } });

        // 5 active subscriptions (at limit)
        var subscriptions = Enumerable.Range(1, 5)
            .Select(i => new Subscription { Id = $"sub-{i}", Status = SubscriptionStatus.Active })
            .ToList();
        
        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscriptions);

        // Act
        var result = await _service.ValidateOperationAsync(userId, TierOperation.AddSubscription);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain("TierLimitExceeded");
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 57: Free tier limit enforcement
    /// Verifies free tier email account limit (1 account)
    /// Validates: Requirements 14.2
    /// </summary>
    [Fact]
    public async Task ValidateOperation_FreeTierAtEmailLimit_ReturnsFailure()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Free };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // 1 active email account (at limit for free tier)
        _mockEmailAccountRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailAccount> { new() { IsActive = true } });

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Subscription>());

        // Act
        var result = await _service.ValidateOperationAsync(userId, TierOperation.AddEmailAccount);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain("TierLimitExceeded");
    }


    /// <summary>
    /// Feature: subscription-tracker, Property 57: Free tier limit enforcement
    /// Verifies free tier users can add subscriptions when under limit
    /// Validates: Requirements 14.2
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ValidateOperation_FreeTierUnderSubscriptionLimit_ReturnsSuccess(int currentCount)
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Free };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockEmailAccountRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailAccount>());

        var subscriptions = Enumerable.Range(1, currentCount)
            .Select(i => new Subscription { Id = $"sub-{i}", Status = SubscriptionStatus.Active })
            .ToList();
        
        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscriptions);

        // Act
        var result = await _service.ValidateOperationAsync(userId, TierOperation.AddSubscription);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 57: Free tier limit enforcement
    /// Verifies free tier limits are correctly defined
    /// Validates: Requirements 14.2
    /// </summary>
    [Fact]
    public void GetTierLimits_FreeTier_ReturnsCorrectLimits()
    {
        // Act
        var limits = _service.GetTierLimits(SubscriptionTier.Free);

        // Assert
        limits.MaxEmailAccounts.Should().Be(1);
        limits.MaxSubscriptions.Should().Be(5);
        limits.HasAiScanning.Should().BeFalse();
        limits.HasCancellationAssistant.Should().BeFalse();
        limits.HasPdfExport.Should().BeFalse();
    }

    #endregion

    #region Task 14.4 - Three-Tier Model: Pro Tier

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Pro tier feature unlock
    /// Verifies Pro tier limits are correctly defined (3 email accounts, unlimited subscriptions)
    /// Validates: Requirements 14.2, 14.3
    /// </summary>
    [Fact]
    public void GetTierLimits_ProTier_ReturnsCorrectLimits()
    {
        // Act
        var limits = _service.GetTierLimits(SubscriptionTier.Pro);

        // Assert
        limits.MaxEmailAccounts.Should().Be(3);
        limits.MaxSubscriptions.Should().Be(int.MaxValue);
        limits.HasAiScanning.Should().BeTrue();
        limits.HasInitial12MonthScan.Should().BeTrue();
        limits.HasRealTimeScanning.Should().BeFalse();
        limits.HasAdvancedFilters.Should().BeTrue();
        limits.HasCustomCategories.Should().BeFalse();
        limits.Has3DayRenewalAlerts.Should().BeTrue();
        limits.HasPriceChangeAlerts.Should().BeTrue();
        limits.HasTrialEndingAlerts.Should().BeTrue();
        limits.HasUnusedSubscriptionAlerts.Should().BeTrue();
        limits.HasCustomAlertTiming.Should().BeFalse();
        limits.HasDailyDigest.Should().BeFalse();
        limits.HasSpendingByCategory.Should().BeTrue();
        limits.HasRenewalTimeline.Should().BeTrue();
        limits.HasSpendingBenchmarks.Should().BeFalse();
        limits.HasSpendingForecasts.Should().BeFalse();
        limits.HasCancellationAssistant.Should().BeFalse();
        limits.HasPdfExport.Should().BeTrue();
        limits.PdfExportLimit.Should().Be(PdfExportLimit.Monthly);
        limits.HasSavingsTracker.Should().BeTrue();
        limits.HasDuplicateDetection.Should().BeFalse();
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Pro tier feature unlock
    /// Verifies Pro tier allows up to 3 email accounts
    /// Validates: Requirements 14.2
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ValidateOperation_ProTierUnderEmailLimit_ReturnsSuccess(int currentCount)
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Pro };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var emailAccounts = Enumerable.Range(1, currentCount)
            .Select(i => new EmailAccount { Id = $"email-{i}", IsActive = true })
            .ToList();
        
        _mockEmailAccountRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emailAccounts);

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Subscription>());

        // Act
        var result = await _service.ValidateOperationAsync(userId, TierOperation.AddEmailAccount);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Pro tier feature unlock
    /// Verifies Pro tier blocks adding 4th email account
    /// Validates: Requirements 14.2
    /// </summary>
    [Fact]
    public async Task ValidateOperation_ProTierAtEmailLimit_ReturnsFailure()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Pro };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // 3 active email accounts (at limit for Pro tier)
        var emailAccounts = Enumerable.Range(1, 3)
            .Select(i => new EmailAccount { Id = $"email-{i}", IsActive = true })
            .ToList();
        
        _mockEmailAccountRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emailAccounts);

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Subscription>());

        // Act
        var result = await _service.ValidateOperationAsync(userId, TierOperation.AddEmailAccount);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain("TierLimitExceeded");
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Pro tier feature unlock
    /// Verifies Pro tier has unlimited subscriptions
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public async Task ValidateOperation_ProTier_AllowsUnlimitedSubscriptions()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Pro };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockEmailAccountRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailAccount>());

        // 100 active subscriptions
        var subscriptions = Enumerable.Range(1, 100)
            .Select(i => new Subscription { Id = $"sub-{i}", Status = SubscriptionStatus.Active })
            .ToList();
        
        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscriptions);

        // Act
        var result = await _service.ValidateOperationAsync(userId, TierOperation.AddSubscription);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Pro tier feature unlock
    /// Verifies Pro tier has AI scanning enabled
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public async Task HasFeatureAccess_ProTier_HasAiScanning()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Pro };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.HasFeatureAccessAsync(userId, TierFeature.AiScanning);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Pro tier feature unlock
    /// Verifies Pro tier does NOT have cancellation assistant
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public async Task HasFeatureAccess_ProTier_NoCancellationAssistant()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Pro };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.HasFeatureAccessAsync(userId, TierFeature.CancellationAssistant);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    #endregion


    #region Task 14.4 - Three-Tier Model: Premium Tier

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Premium tier feature unlock
    /// Verifies Premium tier limits are correctly defined (unlimited everything)
    /// Validates: Requirements 14.2, 14.3
    /// </summary>
    [Fact]
    public void GetTierLimits_PremiumTier_ReturnsUnlimitedLimits()
    {
        // Act
        var limits = _service.GetTierLimits(SubscriptionTier.Premium);

        // Assert
        limits.MaxEmailAccounts.Should().Be(int.MaxValue);
        limits.MaxSubscriptions.Should().Be(int.MaxValue);
        limits.HasAiScanning.Should().BeTrue();
        limits.HasInitial12MonthScan.Should().BeTrue();
        limits.HasRealTimeScanning.Should().BeTrue();
        limits.HasAdvancedFilters.Should().BeTrue();
        limits.HasCustomCategories.Should().BeTrue();
        limits.Has3DayRenewalAlerts.Should().BeTrue();
        limits.HasPriceChangeAlerts.Should().BeTrue();
        limits.HasTrialEndingAlerts.Should().BeTrue();
        limits.HasUnusedSubscriptionAlerts.Should().BeTrue();
        limits.HasCustomAlertTiming.Should().BeTrue();
        limits.HasDailyDigest.Should().BeTrue();
        limits.HasSpendingByCategory.Should().BeTrue();
        limits.HasRenewalTimeline.Should().BeTrue();
        limits.HasSpendingBenchmarks.Should().BeTrue();
        limits.HasSpendingForecasts.Should().BeTrue();
        limits.HasCancellationAssistant.Should().BeTrue();
        limits.HasCancellationTemplates.Should().BeTrue();
        limits.HasPdfExport.Should().BeTrue();
        limits.PdfExportLimit.Should().Be(PdfExportLimit.Unlimited);
        limits.HasSavingsTracker.Should().BeTrue();
        limits.HasDuplicateDetection.Should().BeTrue();
        limits.HasUnlimitedHistory.Should().BeTrue();
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Premium tier feature unlock
    /// Verifies Premium tier has unlimited email accounts
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public async Task ValidateOperation_PremiumTier_AllowsUnlimitedEmailAccounts()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Premium };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // 10 active email accounts
        var emailAccounts = Enumerable.Range(1, 10)
            .Select(i => new EmailAccount { Id = $"email-{i}", IsActive = true })
            .ToList();
        
        _mockEmailAccountRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emailAccounts);

        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Subscription>());

        // Act
        var result = await _service.ValidateOperationAsync(userId, TierOperation.AddEmailAccount);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Premium tier feature unlock
    /// Verifies Premium tier has cancellation assistant
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public async Task HasFeatureAccess_PremiumTier_HasCancellationAssistant()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Premium };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.HasFeatureAccessAsync(userId, TierFeature.CancellationAssistant);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Premium tier feature unlock
    /// Verifies Premium tier has real-time scanning (Premium-only feature)
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public async Task HasFeatureAccess_PremiumTier_HasRealTimeScanning()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Premium };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.HasFeatureAccessAsync(userId, TierFeature.RealTimeScanning);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Premium tier feature unlock
    /// Verifies Pro tier does NOT have real-time scanning
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public async Task HasFeatureAccess_ProTier_NoRealTimeScanning()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Pro };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.HasFeatureAccessAsync(userId, TierFeature.RealTimeScanning);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    #endregion

    #region Task 14.3 - Property 59: Downgrade Data Preservation

    /// <summary>
    /// Feature: subscription-tracker, Property 59: Downgrade data preservation
    /// For any user downgrade from Premium to Free tier, all existing subscription data SHALL be preserved
    /// Validates: Requirements 14.4
    /// </summary>
    [Fact]
    public async Task DowngradeToFree_PreservesAllData()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Premium };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockUserRepository
            .Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DowngradeToFreeAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Tier.Should().Be(SubscriptionTier.Free);
        
        // Verify only tier was updated, no data deletion
        _mockUserRepository.Verify(
            r => r.UpdateAsync(It.Is<User>(u => u.Tier == SubscriptionTier.Free), It.IsAny<CancellationToken>()),
            Times.Once);
        
        // Verify no deletion methods were called
        _mockSubscriptionRepository.Verify(
            r => r.DeleteAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()),
            Times.Never);
        
        _mockEmailAccountRepository.Verify(
            r => r.DeleteAsync(It.IsAny<EmailAccount>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 59: Downgrade data preservation
    /// Verifies downgraded user still has access to view their data
    /// Validates: Requirements 14.4
    /// </summary>
    [Fact]
    public async Task GetUsage_AfterDowngrade_ShowsAllExistingData()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Free };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // User has 3 email accounts (exceeds free tier limit but preserved from paid)
        var emailAccounts = Enumerable.Range(1, 3)
            .Select(i => new EmailAccount { Id = $"email-{i}", IsActive = true })
            .ToList();
        
        _mockEmailAccountRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emailAccounts);

        // User has 10 subscriptions (exceeds free tier limit but preserved from paid)
        var subscriptions = Enumerable.Range(1, 10)
            .Select(i => new Subscription { Id = $"sub-{i}", Status = SubscriptionStatus.Active })
            .ToList();
        
        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscriptions);

        // Act
        var result = await _service.GetUsageAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EmailAccountCount.Should().Be(3); // All preserved
        result.Value.SubscriptionCount.Should().Be(10); // All preserved
        result.Value.CurrentTier.Should().Be(SubscriptionTier.Free);
        result.Value.IsAtEmailLimit.Should().BeTrue(); // At limit (can't add more)
        result.Value.IsAtSubscriptionLimit.Should().BeTrue(); // At limit (can't add more)
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 59: Downgrade data preservation
    /// Verifies downgrade is idempotent (calling twice doesn't cause issues)
    /// Validates: Requirements 14.4
    /// </summary>
    [Fact]
    public async Task DowngradeToFree_AlreadyFree_ReturnsSuccess()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Free };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.DowngradeToFreeAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Should not call update since already free
        _mockUserRepository.Verify(
            r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion


    #region Upgrade Tests

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Tier upgrade
    /// Verifies upgrade from Free to Pro tier
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public async Task UpgradeToTier_FreeToProTier_Success()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Free };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockUserRepository
            .Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpgradeToTierAsync(userId, SubscriptionTier.Pro);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Tier.Should().Be(SubscriptionTier.Pro);
        
        _mockUserRepository.Verify(
            r => r.UpdateAsync(It.Is<User>(u => u.Tier == SubscriptionTier.Pro), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Tier upgrade
    /// Verifies upgrade from Free to Premium tier
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public async Task UpgradeToTier_FreeToPremiumTier_Success()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Free };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockUserRepository
            .Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpgradeToTierAsync(userId, SubscriptionTier.Premium);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Tier.Should().Be(SubscriptionTier.Premium);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Tier upgrade
    /// Verifies upgrade from Pro to Premium tier
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public async Task UpgradeToTier_ProToPremiumTier_Success()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Pro };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockUserRepository
            .Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpgradeToTierAsync(userId, SubscriptionTier.Premium);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Tier.Should().Be(SubscriptionTier.Premium);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Tier upgrade
    /// Verifies upgrade is idempotent
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public async Task UpgradeToTier_AlreadyAtTier_ReturnsSuccess()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Pro };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.UpgradeToTierAsync(userId, SubscriptionTier.Pro);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Should not call update since already at tier
        _mockUserRepository.Verify(
            r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 58: Tier upgrade
    /// Verifies downgrade via UpgradeToTierAsync is blocked
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public async Task UpgradeToTier_DowngradeAttempt_ReturnsFailure()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Premium };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.UpgradeToTierAsync(userId, SubscriptionTier.Pro);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain("downgrade");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Verifies non-existent user returns appropriate error
    /// </summary>
    [Fact]
    public async Task ValidateOperation_NonExistentUser_ReturnsFailure()
    {
        // Arrange
        _mockUserRepository
            .Setup(r => r.GetByIdAsync("non-existent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.ValidateOperationAsync("non-existent", TierOperation.AddSubscription);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// Verifies PendingReview subscriptions count toward limit
    /// </summary>
    [Fact]
    public async Task GetUsage_CountsPendingReviewSubscriptions()
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Free };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockEmailAccountRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailAccount>());

        // Mix of Active and PendingReview subscriptions
        var subscriptions = new List<Subscription>
        {
            new() { Id = "sub-1", Status = SubscriptionStatus.Active },
            new() { Id = "sub-2", Status = SubscriptionStatus.Active },
            new() { Id = "sub-3", Status = SubscriptionStatus.PendingReview },
            new() { Id = "sub-4", Status = SubscriptionStatus.Cancelled }, // Should not count
            new() { Id = "sub-5", Status = SubscriptionStatus.Archived } // Should not count
        };
        
        _mockSubscriptionRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscriptions);

        // Act
        var result = await _service.GetUsageAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubscriptionCount.Should().Be(3); // 2 Active + 1 PendingReview
    }

    /// <summary>
    /// Verifies Free tier does NOT have access to Pro/Premium features
    /// </summary>
    [Theory]
    [InlineData(TierFeature.AiScanning)]
    [InlineData(TierFeature.CancellationAssistant)]
    [InlineData(TierFeature.PdfExport)]
    [InlineData(TierFeature.RealTimeScanning)]
    [InlineData(TierFeature.SpendingByCategory)]
    public async Task HasFeatureAccess_FreeTier_DeniedPaidFeatures(TierFeature feature)
    {
        // Arrange
        var userId = "user-123";
        var user = new User { Id = userId, Tier = SubscriptionTier.Free };
        
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.HasFeatureAccessAsync(userId, feature);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    #endregion
}

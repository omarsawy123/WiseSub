using FluentAssertions;
using Moq;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Services;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Tests.Services;

/// <summary>
/// Property tests for SubscriptionService
/// Covers Task 8.1 (database record creation) and Task 8.2 (billing cycle normalization)
/// </summary>
public class SubscriptionServiceTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepository;
    private readonly SubscriptionService _service;

    public SubscriptionServiceTests()
    {
        _mockRepository = new Mock<ISubscriptionRepository>();
        _service = new SubscriptionService(_mockRepository.Object);
    }

    #region Task 8.1 - Database Record Creation

    /// <summary>
    /// Property Test 8.1: Verify database record is created for extracted subscription
    /// When CreateOrUpdateAsync is called with valid data, a subscription should be created
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateAsync_WithValidRequest_CreatesSubscriptionRecord()
    {
        // Arrange
        var request = new CreateSubscriptionRequest
        {
            UserId = "user-123",
            EmailAccountId = "email-456",
            ServiceName = "Netflix",
            Price = 15.99m,
            Currency = "USD",
            BillingCycle = BillingCycle.Monthly,
            NextRenewalDate = DateTime.UtcNow.AddDays(30),
            Category = "Entertainment",
            ExtractionConfidence = 0.95
        };

        _mockRepository
            .Setup(r => r.FindPotentialDuplicatesAsync(request.UserId, request.ServiceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Subscription>());

        Subscription? capturedSubscription = null;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Callback<Subscription, CancellationToken>((s, _) => capturedSubscription = s)
            .ReturnsAsync((Subscription s, CancellationToken _) => s);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateOrUpdateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedSubscription.Should().NotBeNull();
        capturedSubscription!.ServiceName.Should().Be("Netflix");
        capturedSubscription.Price.Should().Be(15.99m);
        capturedSubscription.UserId.Should().Be("user-123");
        capturedSubscription.EmailAccountId.Should().Be("email-456");
        
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Property Test 8.1: Verify all required fields are captured in the subscription record
    /// </summary>
    [Theory]
    [InlineData("Spotify", 9.99, "USD", BillingCycle.Monthly)]
    [InlineData("Adobe Creative Cloud", 599.88, "USD", BillingCycle.Annual)]
    [InlineData("ChatGPT Plus", 20.00, "USD", BillingCycle.Monthly)]
    public async Task CreateOrUpdateAsync_CapturesAllRequiredFields(
        string serviceName, decimal price, string currency, BillingCycle billingCycle)
    {
        // Arrange
        var request = new CreateSubscriptionRequest
        {
            UserId = "user-123",
            EmailAccountId = "email-456",
            ServiceName = serviceName,
            Price = price,
            Currency = currency,
            BillingCycle = billingCycle,
            ExtractionConfidence = 0.9
        };

        _mockRepository
            .Setup(r => r.FindPotentialDuplicatesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Subscription>());

        Subscription? capturedSubscription = null;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Callback<Subscription, CancellationToken>((s, _) => capturedSubscription = s)
            .ReturnsAsync((Subscription s, CancellationToken _) => s);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateOrUpdateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedSubscription.Should().NotBeNull();
        capturedSubscription!.ServiceName.Should().Be(serviceName);
        capturedSubscription.Price.Should().Be(price);
        capturedSubscription.Currency.Should().Be(currency);
        capturedSubscription.BillingCycle.Should().Be(billingCycle);
    }

    /// <summary>
    /// Property Test 8.1: Low confidence extractions should be marked for review
    /// </summary>
    [Theory]
    [InlineData(0.79, true, SubscriptionStatus.PendingReview)]
    [InlineData(0.80, false, SubscriptionStatus.Active)]
    [InlineData(0.95, false, SubscriptionStatus.Active)]
    [InlineData(0.50, true, SubscriptionStatus.PendingReview)]
    public async Task CreateOrUpdateAsync_SetsReviewFlagBasedOnConfidence(
        double confidence, bool expectedRequiresReview, SubscriptionStatus expectedStatus)
    {
        // Arrange
        var request = new CreateSubscriptionRequest
        {
            UserId = "user-123",
            EmailAccountId = "email-456",
            ServiceName = "Test Service",
            Price = 10.00m,
            BillingCycle = BillingCycle.Monthly,
            ExtractionConfidence = confidence
        };

        _mockRepository
            .Setup(r => r.FindPotentialDuplicatesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Subscription>());

        Subscription? capturedSubscription = null;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Callback<Subscription, CancellationToken>((s, _) => capturedSubscription = s)
            .ReturnsAsync((Subscription s, CancellationToken _) => s);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateOrUpdateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedSubscription!.RequiresUserReview.Should().Be(expectedRequiresReview);
        capturedSubscription.Status.Should().Be(expectedStatus);
    }

    /// <summary>
    /// Property Test 8.1: History entry should be created when subscription is created
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateAsync_CreatesHistoryEntry_WhenSubscriptionCreated()
    {
        // Arrange
        var request = new CreateSubscriptionRequest
        {
            UserId = "user-123",
            EmailAccountId = "email-456",
            ServiceName = "GitHub Copilot",
            Price = 19.00m,
            BillingCycle = BillingCycle.Monthly,
            SourceEmailId = "email-msg-123",
            ExtractionConfidence = 0.95
        };

        _mockRepository
            .Setup(r => r.FindPotentialDuplicatesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Subscription>());

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription s, CancellationToken _) => s);

        Subscription? updatedSubscription = null;
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Callback<Subscription, CancellationToken>((s, _) => updatedSubscription = s)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateOrUpdateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        updatedSubscription!.History.Should().HaveCount(1);
        updatedSubscription.History.First().ChangeType.Should().Be("Created");
        updatedSubscription.History.First().SourceEmailId.Should().Be("email-msg-123");
    }

    #endregion

    #region Task 8.2 - Billing Cycle Normalization

    /// <summary>
    /// Property Test 8.2: Monthly subscriptions should return the same price
    /// </summary>
    [Theory]
    [InlineData(9.99)]
    [InlineData(15.00)]
    [InlineData(29.99)]
    [InlineData(100.00)]
    public void NormalizeToMonthly_MonthlyBilling_ReturnsOriginalPrice(decimal monthlyPrice)
    {
        // Act
        var normalized = _service.NormalizeToMonthly(monthlyPrice, BillingCycle.Monthly);

        // Assert
        normalized.Should().Be(monthlyPrice);
    }

    /// <summary>
    /// Property Test 8.2: Annual subscriptions should be divided by 12
    /// </summary>
    [Theory]
    [InlineData(120.00, 10.00)]
    [InlineData(599.88, 49.99)]
    [InlineData(1200.00, 100.00)]
    [InlineData(0.00, 0.00)]
    public void NormalizeToMonthly_AnnualBilling_DividesBy12(decimal annualPrice, decimal expectedMonthly)
    {
        // Act
        var normalized = _service.NormalizeToMonthly(annualPrice, BillingCycle.Annual);

        // Assert
        normalized.Should().Be(expectedMonthly);
    }

    /// <summary>
    /// Property Test 8.2: Quarterly subscriptions should be divided by 3
    /// </summary>
    [Theory]
    [InlineData(30.00, 10.00)]
    [InlineData(89.97, 29.99)]
    [InlineData(150.00, 50.00)]
    public void NormalizeToMonthly_QuarterlyBilling_DividesBy3(decimal quarterlyPrice, decimal expectedMonthly)
    {
        // Act
        var normalized = _service.NormalizeToMonthly(quarterlyPrice, BillingCycle.Quarterly);

        // Assert
        normalized.Should().Be(expectedMonthly);
    }

    /// <summary>
    /// Property Test 8.2: Weekly subscriptions should be multiplied by 4.33
    /// </summary>
    [Theory]
    [InlineData(10.00, 43.30)]
    [InlineData(5.00, 21.65)]
    [InlineData(0.00, 0.00)]
    public void NormalizeToMonthly_WeeklyBilling_MultipliesBy4_33(decimal weeklyPrice, decimal expectedMonthly)
    {
        // Act
        var normalized = _service.NormalizeToMonthly(weeklyPrice, BillingCycle.Weekly);

        // Assert
        normalized.Should().Be(expectedMonthly);
    }

    /// <summary>
    /// Property Test 8.2: Unknown billing cycle should return original price
    /// </summary>
    [Theory]
    [InlineData(25.00)]
    [InlineData(100.00)]
    public void NormalizeToMonthly_UnknownBilling_ReturnsOriginalPrice(decimal price)
    {
        // Act
        var normalized = _service.NormalizeToMonthly(price, BillingCycle.Unknown);

        // Assert
        normalized.Should().Be(price);
    }

    #endregion

    #region Fuzzy Matching / Deduplication Tests

    /// <summary>
    /// Verify exact match strings return similarity of 1.0
    /// </summary>
    [Theory]
    [InlineData("Netflix", "Netflix")]
    [InlineData("spotify", "spotify")]
    [InlineData("Adobe Creative Cloud", "Adobe Creative Cloud")]
    public void CalculateSimilarity_ExactMatch_ReturnsOne(string source, string target)
    {
        // Act
        var similarity = SubscriptionService.CalculateSimilarity(source, target);

        // Assert
        similarity.Should().Be(1.0);
    }

    /// <summary>
    /// Verify case-insensitive matching
    /// </summary>
    [Theory]
    [InlineData("Netflix", "netflix")]
    [InlineData("SPOTIFY", "spotify")]
    [InlineData("Adobe Creative Cloud", "adobe creative cloud")]
    public void CalculateSimilarity_CaseInsensitive_ReturnsOne(string source, string target)
    {
        // Act
        var similarity = SubscriptionService.CalculateSimilarity(source, target);

        // Assert
        similarity.Should().Be(1.0);
    }

    /// <summary>
    /// Verify similar strings are detected as duplicates (≥85% threshold)
    /// </summary>
    [Theory]
    [InlineData("Netflix", "Netflx", 0.85)] // Missing one character
    [InlineData("Spotify Premium", "Spotify Premium!", 0.85)] // Extra character
    [InlineData("Adobe CC", "Adobe C", 0.85)] // Minor difference
    public void CalculateSimilarity_SimilarStrings_MeetsDuplicateThreshold(
        string source, string target, double minSimilarity)
    {
        // Act
        var similarity = SubscriptionService.CalculateSimilarity(source, target);

        // Assert
        similarity.Should().BeGreaterThanOrEqualTo(minSimilarity);
    }

    /// <summary>
    /// Verify dissimilar strings don't meet duplicate threshold
    /// </summary>
    [Theory]
    [InlineData("Netflix", "Hulu")]
    [InlineData("Spotify", "Apple Music")]
    [InlineData("Adobe CC", "Microsoft 365")]
    public void CalculateSimilarity_DissimilarStrings_BelowThreshold(string source, string target)
    {
        // Act
        var similarity = SubscriptionService.CalculateSimilarity(source, target);

        // Assert
        similarity.Should().BeLessThan(0.85);
    }

    /// <summary>
    /// Verify duplicate detection updates existing subscription instead of creating new
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateAsync_WithDuplicate_UpdatesExistingSubscription()
    {
        // Arrange
        var existingSubscription = new Subscription
        {
            Id = "existing-sub-123",
            UserId = "user-123",
            EmailAccountId = "email-456",
            ServiceName = "Netflix",
            Price = 12.99m,
            Currency = "USD",
            BillingCycle = BillingCycle.Monthly,
            Status = SubscriptionStatus.Active,
            History = new List<SubscriptionHistory>()
        };

        var request = new CreateSubscriptionRequest
        {
            UserId = "user-123",
            EmailAccountId = "email-456",
            ServiceName = "Netflix", // Exact match
            Price = 15.99m, // Price change
            Currency = "USD",
            BillingCycle = BillingCycle.Monthly,
            ExtractionConfidence = 0.95
        };

        _mockRepository
            .Setup(r => r.FindPotentialDuplicatesAsync(request.UserId, request.ServiceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existingSubscription });

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateOrUpdateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("existing-sub-123");
        result.Value.Price.Should().Be(15.99m); // Updated price
        
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verify price change is recorded in history when updating duplicate
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateAsync_WithPriceChange_RecordsHistoryEntry()
    {
        // Arrange
        var existingSubscription = new Subscription
        {
            Id = "existing-sub-123",
            UserId = "user-123",
            EmailAccountId = "email-456",
            ServiceName = "Spotify",
            Price = 9.99m,
            Currency = "USD",
            BillingCycle = BillingCycle.Monthly,
            Status = SubscriptionStatus.Active,
            History = new List<SubscriptionHistory>()
        };

        var request = new CreateSubscriptionRequest
        {
            UserId = "user-123",
            EmailAccountId = "email-456",
            ServiceName = "Spotify",
            Price = 10.99m, // Price increase
            Currency = "USD",
            BillingCycle = BillingCycle.Monthly,
            SourceEmailId = "price-change-email-123",
            ExtractionConfidence = 0.95
        };

        _mockRepository
            .Setup(r => r.FindPotentialDuplicatesAsync(request.UserId, request.ServiceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existingSubscription });

        Subscription? updatedSubscription = null;
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Callback<Subscription, CancellationToken>((s, _) => updatedSubscription = s)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateOrUpdateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        updatedSubscription!.History.Should().Contain(h => 
            h.ChangeType == "PriceChange" && 
            h.OldValue == "9.99 USD" && 
            h.NewValue == "10.99 USD" &&
            h.SourceEmailId == "price-change-email-123");
    }

    #endregion

    #region Status Management Tests

    /// <summary>
    /// Verify status changes are recorded in history
    /// </summary>
    [Fact]
    public async Task UpdateStatusAsync_RecordsStatusChangeInHistory()
    {
        // Arrange
        var subscription = new Subscription
        {
            Id = "sub-123",
            UserId = "user-123",
            ServiceName = "Netflix",
            Status = SubscriptionStatus.Active,
            History = new List<SubscriptionHistory>()
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync("sub-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateStatusAsync("sub-123", SubscriptionStatus.Cancelled, "cancel-email-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
        subscription.CancelledAt.Should().NotBeNull();
        subscription.History.Should().Contain(h => 
            h.ChangeType == "StatusChange" && 
            h.OldValue == "Active" && 
            h.NewValue == "Cancelled");
    }

    /// <summary>
    /// Verify approval removes review flag and sets status to active
    /// </summary>
    [Fact]
    public async Task ApproveSubscriptionAsync_RemovesReviewFlagAndActivates()
    {
        // Arrange
        var subscription = new Subscription
        {
            Id = "sub-123",
            UserId = "user-123",
            ServiceName = "Unknown Service",
            Status = SubscriptionStatus.PendingReview,
            RequiresUserReview = true,
            History = new List<SubscriptionHistory>()
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync("sub-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ApproveSubscriptionAsync("sub-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        subscription.RequiresUserReview.Should().BeFalse();
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.History.Should().Contain(h => h.ChangeType == "Approved");
    }

    /// <summary>
    /// Verify rejection archives the subscription
    /// </summary>
    [Fact]
    public async Task RejectSubscriptionAsync_ArchivesSubscription()
    {
        // Arrange
        var subscription = new Subscription
        {
            Id = "sub-123",
            UserId = "user-123",
            ServiceName = "Unknown Service",
            Status = SubscriptionStatus.PendingReview,
            RequiresUserReview = true,
            History = new List<SubscriptionHistory>()
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync("sub-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.RejectSubscriptionAsync("sub-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        subscription.RequiresUserReview.Should().BeFalse();
        subscription.Status.Should().Be(SubscriptionStatus.Archived);
        subscription.History.Should().Contain(h => h.ChangeType == "Rejected");
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Verify validation fails for empty user ID
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateAsync_WithEmptyUserId_ReturnsValidationError()
    {
        // Arrange
        var request = new CreateSubscriptionRequest
        {
            UserId = "",
            EmailAccountId = "email-456",
            ServiceName = "Netflix",
            Price = 15.99m,
            BillingCycle = BillingCycle.Monthly
        };

        // Act
        var result = await _service.CreateOrUpdateAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(ValidationErrors.Required.Code);
    }

    /// <summary>
    /// Verify validation fails for negative price
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateAsync_WithNegativePrice_ReturnsInvalidPriceError()
    {
        // Arrange
        var request = new CreateSubscriptionRequest
        {
            UserId = "user-123",
            EmailAccountId = "email-456",
            ServiceName = "Netflix",
            Price = -15.99m,
            BillingCycle = BillingCycle.Monthly
        };

        // Act
        var result = await _service.CreateOrUpdateAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(SubscriptionErrors.InvalidPrice.Code);
    }

    /// <summary>
    /// Verify GetByIdAsync returns not found for non-existent subscription
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNotFoundError()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByIdAsync("non-existent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        // Act
        var result = await _service.GetByIdAsync("non-existent");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain(SubscriptionErrors.NotFound.Code);
    }

    #endregion
}

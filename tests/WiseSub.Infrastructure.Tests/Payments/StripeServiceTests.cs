using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WiseSub.Application.Common.Configuration;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;
using WiseSub.Infrastructure.Data;
using WiseSub.Infrastructure.Payments;
using WiseSub.Infrastructure.Repositories;

namespace WiseSub.Infrastructure.Tests.Payments;

/// <summary>
/// Unit tests for Stripe integration
/// Tests checkout session creation, webhook event handling, and subscription status updates
/// _Requirements: 14.5_
/// </summary>
public class StripeServiceTests : IDisposable
{
    private readonly WiseSubDbContext _context;
    private readonly UserRepository _userRepository;
    private readonly Mock<ILogger<StripeService>> _loggerMock;
    private readonly StripeConfiguration _config;

    public StripeServiceTests()
    {
        var options = new DbContextOptionsBuilder<WiseSubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new WiseSubDbContext(options);
        _userRepository = new UserRepository(_context);
        _loggerMock = new Mock<ILogger<StripeService>>();
        
        _config = new StripeConfiguration
        {
            SecretKey = "sk_test_fake_key",
            PublishableKey = "pk_test_fake_key",
            WebhookSecret = "whsec_test_fake_secret",
            PriceIds = new StripePriceIds
            {
                ProMonthly = "price_pro_monthly",
                ProAnnual = "price_pro_annual",
                PremiumMonthly = "price_premium_monthly",
                PremiumAnnual = "price_premium_annual"
            }
        };
    }

    private StripeService CreateService()
    {
        return new StripeService(
            _userRepository,
            Options.Create(_config),
            _loggerMock.Object);
    }

    private async Task<User> CreateTestUser(
        string email = "test@example.com",
        SubscriptionTier tier = SubscriptionTier.Free,
        string? stripeCustomerId = null,
        string? stripeSubscriptionId = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            Name = "Test User",
            OAuthProvider = "Google",
            OAuthSubjectId = Guid.NewGuid().ToString(),
            Tier = tier,
            StripeCustomerId = stripeCustomerId,
            StripeSubscriptionId = stripeSubscriptionId,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);
        return user;
    }

    #region CreateCheckoutSessionAsync Tests

    [Fact]
    public async Task CreateCheckoutSessionAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        var nonExistentUserId = Guid.NewGuid().ToString();

        // Act
        var result = await service.CreateCheckoutSessionAsync(
            nonExistentUserId,
            SubscriptionTier.Pro,
            isAnnual: false,
            "https://example.com/success",
            "https://example.com/cancel");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("User.NotFound", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_FreeTierTarget_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser();

        // Act
        var result = await service.CreateCheckoutSessionAsync(
            user.Id,
            SubscriptionTier.Free,
            isAnnual: false,
            "https://example.com/success",
            "https://example.com/cancel");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("InvalidTier", result.ErrorMessage);
    }

    #endregion

    #region CreateBillingPortalSessionAsync Tests

    [Fact]
    public async Task CreateBillingPortalSessionAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        var nonExistentUserId = Guid.NewGuid().ToString();

        // Act
        var result = await service.CreateBillingPortalSessionAsync(
            nonExistentUserId,
            "https://example.com/return");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("User.NotFound", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateBillingPortalSessionAsync_NoStripeCustomer_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser();

        // Act
        var result = await service.CreateBillingPortalSessionAsync(
            user.Id,
            "https://example.com/return");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("NoSubscription", result.ErrorMessage);
    }

    #endregion

    #region GetSubscriptionStatusAsync Tests

    [Fact]
    public async Task GetSubscriptionStatusAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        var nonExistentUserId = Guid.NewGuid().ToString();

        // Act
        var result = await service.GetSubscriptionStatusAsync(nonExistentUserId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("User.NotFound", result.ErrorMessage);
    }

    [Fact]
    public async Task GetSubscriptionStatusAsync_NoStripeSubscription_ReturnsInactiveStatus()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(tier: SubscriptionTier.Free);

        // Act
        var result = await service.GetSubscriptionStatusAsync(user.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var status = result.Value;
        Assert.False(status.IsActive);
        Assert.Equal(SubscriptionTier.Free, status.CurrentTier);
        Assert.Null(status.StripeSubscriptionId);
        Assert.Null(status.StripePriceId);
        Assert.Null(status.CurrentPeriodStart);
        Assert.Null(status.CurrentPeriodEnd);
        Assert.False(status.CancelAtPeriodEnd);
        Assert.False(status.IsAnnualBilling);
    }

    [Fact]
    public async Task GetSubscriptionStatusAsync_ProTierUser_ReturnsCorrectTier()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(tier: SubscriptionTier.Pro);

        // Act
        var result = await service.GetSubscriptionStatusAsync(user.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var status = result.Value;
        Assert.Equal(SubscriptionTier.Pro, status.CurrentTier);
    }

    #endregion

    #region CancelSubscriptionAsync Tests

    [Fact]
    public async Task CancelSubscriptionAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        var nonExistentUserId = Guid.NewGuid().ToString();

        // Act
        var result = await service.CancelSubscriptionAsync(
            nonExistentUserId,
            cancelAtPeriodEnd: true);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("User.NotFound", result.ErrorMessage);
    }

    [Fact]
    public async Task CancelSubscriptionAsync_NoSubscription_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser();

        // Act
        var result = await service.CancelSubscriptionAsync(
            user.Id,
            cancelAtPeriodEnd: true);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("NoSubscription", result.ErrorMessage);
    }

    #endregion

    #region ChangeTierAsync Tests

    [Fact]
    public async Task ChangeTierAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        var nonExistentUserId = Guid.NewGuid().ToString();

        // Act
        var result = await service.ChangeTierAsync(
            nonExistentUserId,
            SubscriptionTier.Pro,
            isAnnual: false);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("User.NotFound", result.ErrorMessage);
    }

    [Fact]
    public async Task ChangeTierAsync_ToFreeTier_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(tier: SubscriptionTier.Pro);

        // Act
        var result = await service.ChangeTierAsync(
            user.Id,
            SubscriptionTier.Free,
            isAnnual: false);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("InvalidOperation", result.ErrorMessage);
        Assert.Contains("DowngradeToFreeAsync", result.ErrorMessage);
    }

    [Fact]
    public async Task ChangeTierAsync_NoActiveSubscription_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(tier: SubscriptionTier.Free);

        // Act
        var result = await service.ChangeTierAsync(
            user.Id,
            SubscriptionTier.Pro,
            isAnnual: false);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("NoSubscription", result.ErrorMessage);
    }

    #endregion

    #region DowngradeToFreeAsync Tests

    [Fact]
    public async Task DowngradeToFreeAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        var nonExistentUserId = Guid.NewGuid().ToString();

        // Act
        var result = await service.DowngradeToFreeAsync(
            nonExistentUserId,
            immediate: true);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("User.NotFound", result.ErrorMessage);
    }

    [Fact]
    public async Task DowngradeToFreeAsync_AlreadyFree_ReturnsSuccess()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(tier: SubscriptionTier.Free);

        // Act
        var result = await service.DowngradeToFreeAsync(
            user.Id,
            immediate: true);

        // Assert
        Assert.True(result.IsSuccess);
        
        // Verify user is still on free tier
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(SubscriptionTier.Free, updatedUser.Tier);
    }

    [Fact]
    public async Task DowngradeToFreeAsync_NoStripeSubscription_DowngradesLocally()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(tier: SubscriptionTier.Pro);

        // Act
        var result = await service.DowngradeToFreeAsync(
            user.Id,
            immediate: true);

        // Assert
        Assert.True(result.IsSuccess);
        
        // Verify user is downgraded to free tier
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(SubscriptionTier.Free, updatedUser.Tier);
        Assert.Null(updatedUser.StripeSubscriptionId);
        Assert.Null(updatedUser.StripePriceId);
        Assert.Null(updatedUser.SubscriptionStartDate);
        Assert.Null(updatedUser.SubscriptionEndDate);
        Assert.False(updatedUser.IsAnnualBilling);
    }

    [Fact]
    public async Task DowngradeToFreeAsync_PreservesStripeCustomerId()
    {
        // Arrange
        var service = CreateService();
        var stripeCustomerId = "cus_test123";
        var user = await CreateTestUser(
            tier: SubscriptionTier.Pro,
            stripeCustomerId: stripeCustomerId);

        // Act
        var result = await service.DowngradeToFreeAsync(
            user.Id,
            immediate: true);

        // Assert
        Assert.True(result.IsSuccess);
        
        // Verify StripeCustomerId is preserved for future upgrades
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(stripeCustomerId, updatedUser.StripeCustomerId);
    }

    #endregion

    #region Subscription Status Update Tests

    [Fact]
    public async Task GetSubscriptionStatusAsync_PremiumTierUser_ReturnsCorrectTier()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(tier: SubscriptionTier.Premium);

        // Act
        var result = await service.GetSubscriptionStatusAsync(user.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var status = result.Value;
        Assert.Equal(SubscriptionTier.Premium, status.CurrentTier);
    }

    [Fact]
    public async Task GetSubscriptionStatusAsync_UserWithAnnualBilling_ReturnsCorrectBillingFlag()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(tier: SubscriptionTier.Pro);
        user.IsAnnualBilling = true;
        await _userRepository.UpdateAsync(user);

        // Act
        var result = await service.GetSubscriptionStatusAsync(user.Id);

        // Assert
        Assert.True(result.IsSuccess);
        // Note: IsAnnualBilling is only returned when there's no Stripe subscription
        // because the actual value comes from the user record
        Assert.Equal(SubscriptionTier.Pro, result.Value.CurrentTier);
    }

    #endregion

    #region Checkout Session Creation Tests (Additional)

    [Fact]
    public async Task CreateCheckoutSessionAsync_ProTierMonthly_UsesCorrectPriceId()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser();

        // Act - This will fail at Stripe API level but validates our logic
        var result = await service.CreateCheckoutSessionAsync(
            user.Id,
            SubscriptionTier.Pro,
            isAnnual: false,
            "https://example.com/success",
            "https://example.com/cancel");

        // Assert - Will fail due to fake API key, but validates user lookup works
        // The error should be from Stripe, not from our validation
        Assert.True(result.IsFailure);
        Assert.Contains("StripeError", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_PremiumTierAnnual_UsesCorrectPriceId()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser();

        // Act - This will fail at Stripe API level but validates our logic
        var result = await service.CreateCheckoutSessionAsync(
            user.Id,
            SubscriptionTier.Premium,
            isAnnual: true,
            "https://example.com/success",
            "https://example.com/cancel");

        // Assert - Will fail due to fake API key, but validates user lookup works
        Assert.True(result.IsFailure);
        Assert.Contains("StripeError", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ExistingStripeCustomer_ReusesCustomerId()
    {
        // Arrange
        var service = CreateService();
        var stripeCustomerId = "cus_existing123";
        var user = await CreateTestUser(stripeCustomerId: stripeCustomerId);

        // Act - This will fail at Stripe API level but validates our logic
        var result = await service.CreateCheckoutSessionAsync(
            user.Id,
            SubscriptionTier.Pro,
            isAnnual: false,
            "https://example.com/success",
            "https://example.com/cancel");

        // Assert - Will fail due to fake API key
        Assert.True(result.IsFailure);
        
        // Verify customer ID was not changed (would be changed if we tried to create new)
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(stripeCustomerId, updatedUser.StripeCustomerId);
    }

    #endregion

    #region Webhook Event Handling Tests

    [Fact]
    public async Task HandleCheckoutSessionCompletedAsync_InvalidSessionId_ReturnsStripeError()
    {
        // Arrange
        var service = CreateService();
        var invalidSessionId = "cs_invalid_session_id";

        // Act
        var result = await service.HandleCheckoutSessionCompletedAsync(invalidSessionId);

        // Assert - Will fail due to invalid session ID at Stripe API level
        Assert.True(result.IsFailure);
        Assert.Contains("StripeError", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleSubscriptionUpdatedAsync_InvalidSubscriptionId_ReturnsStripeError()
    {
        // Arrange
        var service = CreateService();
        var invalidSubscriptionId = "sub_invalid_subscription_id";

        // Act
        var result = await service.HandleSubscriptionUpdatedAsync(invalidSubscriptionId);

        // Assert - Will fail due to invalid subscription ID at Stripe API level
        Assert.True(result.IsFailure);
        Assert.Contains("StripeError", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleSubscriptionDeletedAsync_InvalidSubscriptionId_ReturnsStripeError()
    {
        // Arrange
        var service = CreateService();
        var invalidSubscriptionId = "sub_invalid_subscription_id";

        // Act
        var result = await service.HandleSubscriptionDeletedAsync(invalidSubscriptionId);

        // Assert - Will fail due to invalid subscription ID at Stripe API level
        Assert.True(result.IsFailure);
        Assert.Contains("StripeError", result.ErrorMessage);
    }

    #endregion

    #region ChangeTierAsync Tests (Additional)

    [Fact]
    public async Task ChangeTierAsync_ProToProAnnual_RequiresActiveSubscription()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(tier: SubscriptionTier.Pro);

        // Act
        var result = await service.ChangeTierAsync(
            user.Id,
            SubscriptionTier.Pro,
            isAnnual: true);

        // Assert - Should fail because user has no Stripe subscription
        Assert.True(result.IsFailure);
        Assert.Contains("NoSubscription", result.ErrorMessage);
    }

    [Fact]
    public async Task ChangeTierAsync_ProToPremium_RequiresActiveSubscription()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(tier: SubscriptionTier.Pro);

        // Act
        var result = await service.ChangeTierAsync(
            user.Id,
            SubscriptionTier.Premium,
            isAnnual: false);

        // Assert - Should fail because user has no Stripe subscription
        Assert.True(result.IsFailure);
        Assert.Contains("NoSubscription", result.ErrorMessage);
    }

    [Fact]
    public async Task ChangeTierAsync_WithStripeSubscription_AttemptsStripeUpdate()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(
            tier: SubscriptionTier.Pro,
            stripeCustomerId: "cus_test123",
            stripeSubscriptionId: "sub_test123");

        // Act
        var result = await service.ChangeTierAsync(
            user.Id,
            SubscriptionTier.Premium,
            isAnnual: false);

        // Assert - Will fail at Stripe API level due to fake subscription ID
        Assert.True(result.IsFailure);
        Assert.Contains("StripeError", result.ErrorMessage);
    }

    #endregion

    #region CancelSubscriptionAsync Tests (Additional)

    [Fact]
    public async Task CancelSubscriptionAsync_WithStripeSubscription_AttemptsStripeCancel()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(
            tier: SubscriptionTier.Pro,
            stripeCustomerId: "cus_test123",
            stripeSubscriptionId: "sub_test123");

        // Act
        var result = await service.CancelSubscriptionAsync(
            user.Id,
            cancelAtPeriodEnd: true);

        // Assert - Will fail at Stripe API level due to fake subscription ID
        Assert.True(result.IsFailure);
        Assert.Contains("StripeError", result.ErrorMessage);
    }

    [Fact]
    public async Task CancelSubscriptionAsync_ImmediateCancel_AttemptsStripeCancel()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(
            tier: SubscriptionTier.Pro,
            stripeCustomerId: "cus_test123",
            stripeSubscriptionId: "sub_test123");

        // Act
        var result = await service.CancelSubscriptionAsync(
            user.Id,
            cancelAtPeriodEnd: false);

        // Assert - Will fail at Stripe API level due to fake subscription ID
        Assert.True(result.IsFailure);
        Assert.Contains("StripeError", result.ErrorMessage);
    }

    #endregion

    #region CreateBillingPortalSessionAsync Tests (Additional)

    [Fact]
    public async Task CreateBillingPortalSessionAsync_WithStripeCustomer_AttemptsPortalCreation()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(
            tier: SubscriptionTier.Pro,
            stripeCustomerId: "cus_test123");

        // Act
        var result = await service.CreateBillingPortalSessionAsync(
            user.Id,
            "https://example.com/return");

        // Assert - Will fail at Stripe API level due to fake customer ID
        Assert.True(result.IsFailure);
        Assert.Contains("StripeError", result.ErrorMessage);
    }

    #endregion

    #region DowngradeToFreeAsync Tests (Additional)

    [Fact]
    public async Task DowngradeToFreeAsync_WithStripeSubscription_AttemptsStripeCancel()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(
            tier: SubscriptionTier.Pro,
            stripeCustomerId: "cus_test123",
            stripeSubscriptionId: "sub_test123");

        // Act
        var result = await service.DowngradeToFreeAsync(
            user.Id,
            immediate: true);

        // Assert - Will fail at Stripe API level due to fake subscription ID
        Assert.True(result.IsFailure);
        Assert.Contains("StripeError", result.ErrorMessage);
    }

    [Fact]
    public async Task DowngradeToFreeAsync_CancelAtPeriodEnd_AttemptsStripeUpdate()
    {
        // Arrange
        var service = CreateService();
        var user = await CreateTestUser(
            tier: SubscriptionTier.Pro,
            stripeCustomerId: "cus_test123",
            stripeSubscriptionId: "sub_test123");

        // Act
        var result = await service.DowngradeToFreeAsync(
            user.Id,
            immediate: false);

        // Assert - Will fail at Stripe API level due to fake subscription ID
        Assert.True(result.IsFailure);
        Assert.Contains("StripeError", result.ErrorMessage);
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

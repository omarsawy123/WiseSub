using CsCheck;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Services;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Tests.Services;

/// <summary>
/// Property-based tests for tier feature access
/// Task 14.9 - Property 60: Feature access by tier
/// Validates: Requirements 14.2, 14.3
/// </summary>
public class FeatureAccessPropertyTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IEmailAccountRepository> _mockEmailAccountRepository;
    private readonly Mock<ISubscriptionRepository> _mockSubscriptionRepository;
    private readonly Mock<ILogger<TierService>> _mockTierLogger;
    private readonly Mock<ILogger<FeatureAccessService>> _mockFeatureLogger;
    private readonly TierService _tierService;
    private readonly FeatureAccessService _featureAccessService;

    // Pro+ features (available in Pro and Premium)
    private static readonly TierFeature[] ProFeatures = new[]
    {
        TierFeature.AiScanning,
        TierFeature.Initial12MonthScan,
        TierFeature.AdvancedFilters,
        TierFeature.ThreeDayRenewalAlerts,
        TierFeature.PriceChangeAlerts,
        TierFeature.TrialEndingAlerts,
        TierFeature.UnusedSubscriptionAlerts,
        TierFeature.SpendingByCategory,
        TierFeature.RenewalTimeline,
        TierFeature.PdfExport,
        TierFeature.SavingsTracker
    };

    // Premium-only features
    private static readonly TierFeature[] PremiumOnlyFeatures = new[]
    {
        TierFeature.RealTimeScanning,
        TierFeature.CustomCategories,
        TierFeature.CustomAlertTiming,
        TierFeature.DailyDigest,
        TierFeature.SpendingBenchmarks,
        TierFeature.SpendingForecasts,
        TierFeature.CancellationAssistant,
        TierFeature.CancellationTemplates,
        TierFeature.DuplicateDetection
    };

    public FeatureAccessPropertyTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockEmailAccountRepository = new Mock<IEmailAccountRepository>();
        _mockSubscriptionRepository = new Mock<ISubscriptionRepository>();
        _mockTierLogger = new Mock<ILogger<TierService>>();
        _mockFeatureLogger = new Mock<ILogger<FeatureAccessService>>();

        _tierService = new TierService(
            _mockUserRepository.Object,
            _mockEmailAccountRepository.Object,
            _mockSubscriptionRepository.Object,
            _mockTierLogger.Object);

        _featureAccessService = new FeatureAccessService(
            _tierService,
            _mockUserRepository.Object,
            _mockFeatureLogger.Object);
    }

    #region Property 60: Feature access by tier

    /// <summary>
    /// Feature: subscription-tracker, Property 60: Feature access by tier
    /// *For any* Pro+ feature, Free tier users SHALL NOT have access
    /// Validates: Requirements 14.2
    /// </summary>
    [Fact]
    public void Property60_FreeTierCannotAccessProFeatures()
    {
        // Feature: subscription-tracker, Property 60: Feature access by tier
        // For any Pro+ feature, Free tier users SHALL NOT have access
        // Validates: Requirements 14.2

        Gen.OneOfConst(ProFeatures)
            .Sample(feature =>
            {
                // Arrange
                var userId = Guid.NewGuid().ToString();
                var user = new User { Id = userId, Tier = SubscriptionTier.Free };

                _mockUserRepository
                    .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(user);

                // Act
                var result = _tierService.HasFeatureAccessAsync(userId, feature).GetAwaiter().GetResult();

                // Assert - Free tier should NOT have access to Pro+ features
                result.IsSuccess.Should().BeTrue($"Feature check for {feature} should succeed");
                result.Value.Should().BeFalse($"Free tier should NOT have access to {feature}");
            }, iter: 100);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 60: Feature access by tier
    /// *For any* Premium-only feature, Free tier users SHALL NOT have access
    /// Validates: Requirements 14.2
    /// </summary>
    [Fact]
    public void Property60_FreeTierCannotAccessPremiumFeatures()
    {
        // Feature: subscription-tracker, Property 60: Feature access by tier
        // For any Premium-only feature, Free tier users SHALL NOT have access
        // Validates: Requirements 14.2

        Gen.OneOfConst(PremiumOnlyFeatures)
            .Sample(feature =>
            {
                // Arrange
                var userId = Guid.NewGuid().ToString();
                var user = new User { Id = userId, Tier = SubscriptionTier.Free };

                _mockUserRepository
                    .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(user);

                // Act
                var result = _tierService.HasFeatureAccessAsync(userId, feature).GetAwaiter().GetResult();

                // Assert - Free tier should NOT have access to Premium-only features
                result.IsSuccess.Should().BeTrue($"Feature check for {feature} should succeed");
                result.Value.Should().BeFalse($"Free tier should NOT have access to {feature}");
            }, iter: 100);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 60: Feature access by tier
    /// *For any* Pro+ feature, Pro tier users SHALL have access
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public void Property60_ProTierCanAccessProFeatures()
    {
        // Feature: subscription-tracker, Property 60: Feature access by tier
        // For any Pro+ feature, Pro tier users SHALL have access
        // Validates: Requirements 14.3

        Gen.OneOfConst(ProFeatures)
            .Sample(feature =>
            {
                // Arrange
                var userId = Guid.NewGuid().ToString();
                var user = new User { Id = userId, Tier = SubscriptionTier.Pro };

                _mockUserRepository
                    .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(user);

                // Act
                var result = _tierService.HasFeatureAccessAsync(userId, feature).GetAwaiter().GetResult();

                // Assert - Pro tier should have access to Pro+ features
                result.IsSuccess.Should().BeTrue($"Feature check for {feature} should succeed");
                result.Value.Should().BeTrue($"Pro tier should have access to {feature}");
            }, iter: 100);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 60: Feature access by tier
    /// *For any* Premium-only feature, Pro tier users SHALL NOT have access
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public void Property60_ProTierCannotAccessPremiumOnlyFeatures()
    {
        // Feature: subscription-tracker, Property 60: Feature access by tier
        // For any Premium-only feature, Pro tier users SHALL NOT have access
        // Validates: Requirements 14.3

        Gen.OneOfConst(PremiumOnlyFeatures)
            .Sample(feature =>
            {
                // Arrange
                var userId = Guid.NewGuid().ToString();
                var user = new User { Id = userId, Tier = SubscriptionTier.Pro };

                _mockUserRepository
                    .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(user);

                // Act
                var result = _tierService.HasFeatureAccessAsync(userId, feature).GetAwaiter().GetResult();

                // Assert - Pro tier should NOT have access to Premium-only features
                result.IsSuccess.Should().BeTrue($"Feature check for {feature} should succeed");
                result.Value.Should().BeFalse($"Pro tier should NOT have access to {feature}");
            }, iter: 100);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 60: Feature access by tier
    /// *For any* Pro+ feature, Premium tier users SHALL have access
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public void Property60_PremiumTierCanAccessProFeatures()
    {
        // Feature: subscription-tracker, Property 60: Feature access by tier
        // For any Pro+ feature, Premium tier users SHALL have access
        // Validates: Requirements 14.3

        Gen.OneOfConst(ProFeatures)
            .Sample(feature =>
            {
                // Arrange
                var userId = Guid.NewGuid().ToString();
                var user = new User { Id = userId, Tier = SubscriptionTier.Premium };

                _mockUserRepository
                    .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(user);

                // Act
                var result = _tierService.HasFeatureAccessAsync(userId, feature).GetAwaiter().GetResult();

                // Assert - Premium tier should have access to Pro+ features
                result.IsSuccess.Should().BeTrue($"Feature check for {feature} should succeed");
                result.Value.Should().BeTrue($"Premium tier should have access to {feature}");
            }, iter: 100);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 60: Feature access by tier
    /// *For any* Premium-only feature, Premium tier users SHALL have access
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public void Property60_PremiumTierCanAccessPremiumOnlyFeatures()
    {
        // Feature: subscription-tracker, Property 60: Feature access by tier
        // For any Premium-only feature, Premium tier users SHALL have access
        // Validates: Requirements 14.3

        Gen.OneOfConst(PremiumOnlyFeatures)
            .Sample(feature =>
            {
                // Arrange
                var userId = Guid.NewGuid().ToString();
                var user = new User { Id = userId, Tier = SubscriptionTier.Premium };

                _mockUserRepository
                    .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(user);

                // Act
                var result = _tierService.HasFeatureAccessAsync(userId, feature).GetAwaiter().GetResult();

                // Assert - Premium tier should have access to Premium-only features
                result.IsSuccess.Should().BeTrue($"Feature check for {feature} should succeed");
                result.Value.Should().BeTrue($"Premium tier should have access to {feature}");
            }, iter: 100);
    }

    #endregion

    #region Property 60: FeatureAccessService Integration Tests

    /// <summary>
    /// Feature: subscription-tracker, Property 60: Feature access by tier
    /// *For any* tier, the feature access summary SHALL correctly reflect all feature access
    /// Validates: Requirements 14.2, 14.3
    /// </summary>
    [Fact]
    public void Property60_FeatureAccessSummaryReflectsCorrectAccess()
    {
        // Feature: subscription-tracker, Property 60: Feature access by tier
        // For any tier, the feature access summary SHALL correctly reflect all feature access
        // Validates: Requirements 14.2, 14.3

        var allTiers = new[] { SubscriptionTier.Free, SubscriptionTier.Pro, SubscriptionTier.Premium };

        Gen.OneOfConst(allTiers)
            .Sample(tier =>
            {
                // Arrange
                var userId = Guid.NewGuid().ToString();
                var user = new User { Id = userId, Tier = tier };

                _mockUserRepository
                    .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(user);

                // Act
                var result = _featureAccessService.GetFeatureAccessSummaryAsync(userId).GetAwaiter().GetResult();

                // Assert
                result.IsSuccess.Should().BeTrue();
                var summary = result.Value;
                summary.CurrentTier.Should().Be(tier);

                // Verify tier-specific access
                if (tier == SubscriptionTier.Free)
                {
                    summary.CanUseAiEmailScanning.Should().BeFalse();
                    summary.CanUseRealTimeScanning.Should().BeFalse();
                    summary.CanUseCancellationAssistant.Should().BeFalse();
                }
                else if (tier == SubscriptionTier.Pro)
                {
                    summary.CanUseAiEmailScanning.Should().BeTrue();
                    summary.CanUseRealTimeScanning.Should().BeFalse();
                    summary.CanUseCancellationAssistant.Should().BeFalse();
                }
                else // Premium
                {
                    summary.CanUseAiEmailScanning.Should().BeTrue();
                    summary.CanUseRealTimeScanning.Should().BeTrue();
                    summary.CanUseCancellationAssistant.Should().BeTrue();
                }
            }, iter: 100);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 60: Feature access by tier
    /// *For any* feature and tier combination, IsFeatureAvailableForTier SHALL return consistent results
    /// Validates: Requirements 14.2, 14.3
    /// </summary>
    [Fact]
    public void Property60_IsFeatureAvailableForTierIsConsistent()
    {
        // Feature: subscription-tracker, Property 60: Feature access by tier
        // For any feature and tier combination, IsFeatureAvailableForTier SHALL return consistent results
        // Validates: Requirements 14.2, 14.3

        var allFeatures = ProFeatures.Concat(PremiumOnlyFeatures).ToArray();
        var allTiers = new[] { SubscriptionTier.Free, SubscriptionTier.Pro, SubscriptionTier.Premium };

        Gen.Select(Gen.OneOfConst(allFeatures), Gen.OneOfConst(allTiers))
            .Sample(tuple =>
            {
                var (feature, tier) = tuple;

                // Act
                var result = _featureAccessService.IsFeatureAvailableForTier(feature, tier);

                // Assert - Verify consistency with tier hierarchy
                if (tier == SubscriptionTier.Free)
                {
                    // Free tier should not have access to any Pro+ or Premium features
                    result.Should().BeFalse($"Free tier should not have access to {feature}");
                }
                else if (tier == SubscriptionTier.Pro)
                {
                    // Pro tier should have access to Pro features but not Premium-only
                    if (ProFeatures.Contains(feature))
                    {
                        result.Should().BeTrue($"Pro tier should have access to Pro feature {feature}");
                    }
                    else if (PremiumOnlyFeatures.Contains(feature))
                    {
                        result.Should().BeFalse($"Pro tier should not have access to Premium-only feature {feature}");
                    }
                }
                else // Premium
                {
                    // Premium tier should have access to all features
                    result.Should().BeTrue($"Premium tier should have access to {feature}");
                }
            }, iter: 100);
    }

    /// <summary>
    /// Feature: subscription-tracker, Property 60: Feature access by tier
    /// *For any* feature, GetMinimumTierForFeature SHALL return a tier that grants access
    /// Validates: Requirements 14.2, 14.3
    /// </summary>
    [Fact]
    public void Property60_MinimumTierGrantsAccess()
    {
        // Feature: subscription-tracker, Property 60: Feature access by tier
        // For any feature, GetMinimumTierForFeature SHALL return a tier that grants access
        // Validates: Requirements 14.2, 14.3

        var allFeatures = ProFeatures.Concat(PremiumOnlyFeatures).ToArray();

        Gen.OneOfConst(allFeatures)
            .Sample(feature =>
            {
                // Act
                var minimumTier = _featureAccessService.GetMinimumTierForFeature(feature);
                var hasAccess = _featureAccessService.IsFeatureAvailableForTier(feature, minimumTier);

                // Assert - The minimum tier should grant access to the feature
                hasAccess.Should().BeTrue($"Minimum tier {minimumTier} should grant access to {feature}");

                // Also verify that lower tiers don't have access
                if (minimumTier == SubscriptionTier.Pro)
                {
                    var freeHasAccess = _featureAccessService.IsFeatureAvailableForTier(feature, SubscriptionTier.Free);
                    freeHasAccess.Should().BeFalse($"Free tier should not have access to {feature} (minimum is Pro)");
                }
                else if (minimumTier == SubscriptionTier.Premium)
                {
                    var freeHasAccess = _featureAccessService.IsFeatureAvailableForTier(feature, SubscriptionTier.Free);
                    var proHasAccess = _featureAccessService.IsFeatureAvailableForTier(feature, SubscriptionTier.Pro);
                    freeHasAccess.Should().BeFalse($"Free tier should not have access to {feature} (minimum is Premium)");
                    proHasAccess.Should().BeFalse($"Pro tier should not have access to {feature} (minimum is Premium)");
                }
            }, iter: 100);
    }

    #endregion
}

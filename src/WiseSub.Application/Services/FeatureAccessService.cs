using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Common;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Services;

/// <summary>
/// Centralized service for feature access control based on subscription tiers
/// </summary>
public class FeatureAccessService : IFeatureAccessService
{
    private readonly ITierService _tierService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<FeatureAccessService> _logger;

    // Feature to minimum tier mapping
    private static readonly Dictionary<TierFeature, SubscriptionTier> FeatureTierRequirements = new()
    {
        // Pro+ features (available in Pro and Premium)
        { TierFeature.AiScanning, SubscriptionTier.Pro },
        { TierFeature.Initial12MonthScan, SubscriptionTier.Pro },
        { TierFeature.AdvancedFilters, SubscriptionTier.Pro },
        { TierFeature.ThreeDayRenewalAlerts, SubscriptionTier.Pro },
        { TierFeature.PriceChangeAlerts, SubscriptionTier.Pro },
        { TierFeature.TrialEndingAlerts, SubscriptionTier.Pro },
        { TierFeature.UnusedSubscriptionAlerts, SubscriptionTier.Pro },
        { TierFeature.SpendingByCategory, SubscriptionTier.Pro },
        { TierFeature.RenewalTimeline, SubscriptionTier.Pro },
        { TierFeature.PdfExport, SubscriptionTier.Pro },
        { TierFeature.SavingsTracker, SubscriptionTier.Pro },

        // Premium only features
        { TierFeature.RealTimeScanning, SubscriptionTier.Premium },
        { TierFeature.CustomCategories, SubscriptionTier.Premium },
        { TierFeature.CustomAlertTiming, SubscriptionTier.Premium },
        { TierFeature.DailyDigest, SubscriptionTier.Premium },
        { TierFeature.SpendingBenchmarks, SubscriptionTier.Premium },
        { TierFeature.SpendingForecasts, SubscriptionTier.Premium },
        { TierFeature.CancellationAssistant, SubscriptionTier.Premium },
        { TierFeature.CancellationTemplates, SubscriptionTier.Premium },
        { TierFeature.DuplicateDetection, SubscriptionTier.Premium }
    };

    public FeatureAccessService(
        ITierService tierService,
        IUserRepository userRepository,
        ILogger<FeatureAccessService> logger)
    {
        _tierService = tierService;
        _userRepository = userRepository;
        _logger = logger;
    }


    /// <inheritdoc />
    public async Task<Result<bool>> CanUseAiEmailScanningAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.AiScanning, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseInitial12MonthScanAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.Initial12MonthScan, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseRealTimeScanningAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.RealTimeScanning, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseAdvancedDashboardFiltersAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.AdvancedFilters, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseCustomCategoriesAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.CustomCategories, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUse3DayRenewalAlertsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.ThreeDayRenewalAlerts, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUsePriceChangeAlertsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.PriceChangeAlerts, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseTrialEndingAlertsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.TrialEndingAlerts, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseUnusedSubscriptionAlertsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.UnusedSubscriptionAlerts, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseCustomAlertTimingAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.CustomAlertTiming, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseDailyDigestAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.DailyDigest, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseSpendingByCategoryAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.SpendingByCategory, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseRenewalTimelineAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.RenewalTimeline, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseSpendingBenchmarksAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.SpendingBenchmarks, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseSpendingForecastsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.SpendingForecasts, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseCancellationAssistantAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.CancellationAssistant, cancellationToken);
    }


    /// <inheritdoc />
    public async Task<Result<PdfExportAccess>> GetPdfExportAccessAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<PdfExportAccess>(UserErrors.NotFound);
        }

        var limits = _tierService.GetTierLimits(user.Tier);
        
        var access = new PdfExportAccess(
            HasAccess: limits.HasPdfExport,
            Limit: limits.PdfExportLimit,
            ExportsThisMonth: null, // TODO: Track actual exports when implementing PDF export
            ExportsRemaining: limits.PdfExportLimit == PdfExportLimit.Monthly ? 1 : null);

        return Result.Success(access);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseSavingsTrackerAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.SavingsTracker, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanUseDuplicateDetectionAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _tierService.HasFeatureAccessAsync(userId, TierFeature.DuplicateDetection, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<FeatureAccessSummary>> GetFeatureAccessSummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<FeatureAccessSummary>(UserErrors.NotFound);
        }

        var limits = _tierService.GetTierLimits(user.Tier);
        
        var pdfAccess = new PdfExportAccess(
            HasAccess: limits.HasPdfExport,
            Limit: limits.PdfExportLimit,
            ExportsThisMonth: null,
            ExportsRemaining: limits.PdfExportLimit == PdfExportLimit.Monthly ? 1 : null);

        var summary = new FeatureAccessSummary(
            CurrentTier: user.Tier,
            // Scanning features
            CanUseAiEmailScanning: limits.HasAiScanning,
            CanUseInitial12MonthScan: limits.HasInitial12MonthScan,
            CanUseRealTimeScanning: limits.HasRealTimeScanning,
            // Dashboard features
            CanUseAdvancedDashboardFilters: limits.HasAdvancedFilters,
            CanUseCustomCategories: limits.HasCustomCategories,
            // Alert features
            CanUse3DayRenewalAlerts: limits.Has3DayRenewalAlerts,
            CanUsePriceChangeAlerts: limits.HasPriceChangeAlerts,
            CanUseTrialEndingAlerts: limits.HasTrialEndingAlerts,
            CanUseUnusedSubscriptionAlerts: limits.HasUnusedSubscriptionAlerts,
            CanUseCustomAlertTiming: limits.HasCustomAlertTiming,
            CanUseDailyDigest: limits.HasDailyDigest,
            // Insight features
            CanUseSpendingByCategory: limits.HasSpendingByCategory,
            CanUseRenewalTimeline: limits.HasRenewalTimeline,
            CanUseSpendingBenchmarks: limits.HasSpendingBenchmarks,
            CanUseSpendingForecasts: limits.HasSpendingForecasts,
            // Tool features
            CanUseCancellationAssistant: limits.HasCancellationAssistant,
            PdfExportAccess: pdfAccess,
            CanUseSavingsTracker: limits.HasSavingsTracker,
            CanUseDuplicateDetection: limits.HasDuplicateDetection);

        _logger.LogDebug("Feature access summary for user {UserId}: Tier={Tier}", userId, user.Tier);
        return Result.Success(summary);
    }

    /// <inheritdoc />
    public bool IsFeatureAvailableForTier(TierFeature feature, SubscriptionTier tier)
    {
        var limits = _tierService.GetTierLimits(tier);
        
        return feature switch
        {
            TierFeature.AiScanning => limits.HasAiScanning,
            TierFeature.Initial12MonthScan => limits.HasInitial12MonthScan,
            TierFeature.RealTimeScanning => limits.HasRealTimeScanning,
            TierFeature.AdvancedFilters => limits.HasAdvancedFilters,
            TierFeature.CustomCategories => limits.HasCustomCategories,
            TierFeature.ThreeDayRenewalAlerts => limits.Has3DayRenewalAlerts,
            TierFeature.PriceChangeAlerts => limits.HasPriceChangeAlerts,
            TierFeature.TrialEndingAlerts => limits.HasTrialEndingAlerts,
            TierFeature.UnusedSubscriptionAlerts => limits.HasUnusedSubscriptionAlerts,
            TierFeature.CustomAlertTiming => limits.HasCustomAlertTiming,
            TierFeature.DailyDigest => limits.HasDailyDigest,
            TierFeature.SpendingByCategory => limits.HasSpendingByCategory,
            TierFeature.RenewalTimeline => limits.HasRenewalTimeline,
            TierFeature.SpendingBenchmarks => limits.HasSpendingBenchmarks,
            TierFeature.SpendingForecasts => limits.HasSpendingForecasts,
            TierFeature.CancellationAssistant => limits.HasCancellationAssistant,
            TierFeature.CancellationTemplates => limits.HasCancellationTemplates,
            TierFeature.PdfExport => limits.HasPdfExport,
            TierFeature.SavingsTracker => limits.HasSavingsTracker,
            TierFeature.DuplicateDetection => limits.HasDuplicateDetection,
            TierFeature.UnlimitedEmailAccounts => limits.MaxEmailAccounts == int.MaxValue,
            TierFeature.UnlimitedSubscriptions => limits.MaxSubscriptions == int.MaxValue,
            TierFeature.AdvancedInsights => limits.HasSpendingByCategory && limits.HasRenewalTimeline,
            _ => false
        };
    }

    /// <inheritdoc />
    public SubscriptionTier GetMinimumTierForFeature(TierFeature feature)
    {
        if (FeatureTierRequirements.TryGetValue(feature, out var tier))
        {
            return tier;
        }

        // For legacy features, determine based on tier limits
        return feature switch
        {
            TierFeature.UnlimitedEmailAccounts => SubscriptionTier.Premium,
            TierFeature.UnlimitedSubscriptions => SubscriptionTier.Pro,
            TierFeature.AdvancedInsights => SubscriptionTier.Pro,
            _ => SubscriptionTier.Free
        };
    }
}

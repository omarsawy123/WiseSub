using WiseSub.Domain.Common;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Service interface for centralized feature access control based on subscription tiers
/// </summary>
public interface IFeatureAccessService
{
    /// <summary>
    /// Checks if a user has access to AI email scanning (Pro+)
    /// </summary>
    Task<Result<bool>> CanUseAiEmailScanningAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to initial 12-month scan (Pro+)
    /// </summary>
    Task<Result<bool>> CanUseInitial12MonthScanAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to real-time scanning (Premium only)
    /// </summary>
    Task<Result<bool>> CanUseRealTimeScanningAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to advanced dashboard filters (Pro+)
    /// </summary>
    Task<Result<bool>> CanUseAdvancedDashboardFiltersAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to custom categories (Premium only)
    /// </summary>
    Task<Result<bool>> CanUseCustomCategoriesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to 3-day renewal alerts (Pro+)
    /// </summary>
    Task<Result<bool>> CanUse3DayRenewalAlertsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to price change alerts (Pro+)
    /// </summary>
    Task<Result<bool>> CanUsePriceChangeAlertsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to trial ending alerts (Pro+)
    /// </summary>
    Task<Result<bool>> CanUseTrialEndingAlertsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to unused subscription alerts (Pro+)
    /// </summary>
    Task<Result<bool>> CanUseUnusedSubscriptionAlertsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to custom alert timing (Premium only)
    /// </summary>
    Task<Result<bool>> CanUseCustomAlertTimingAsync(string userId, CancellationToken cancellationToken = default);


    /// <summary>
    /// Checks if a user has access to daily digest option (Premium only)
    /// </summary>
    Task<Result<bool>> CanUseDailyDigestAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to spending by category insights (Pro+)
    /// </summary>
    Task<Result<bool>> CanUseSpendingByCategoryAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to renewal timeline (Pro+)
    /// </summary>
    Task<Result<bool>> CanUseRenewalTimelineAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to spending benchmarks (Premium only)
    /// </summary>
    Task<Result<bool>> CanUseSpendingBenchmarksAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to spending forecasts (Premium only)
    /// </summary>
    Task<Result<bool>> CanUseSpendingForecastsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to cancellation assistant (Premium only)
    /// </summary>
    Task<Result<bool>> CanUseCancellationAssistantAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to PDF export and returns the limit type
    /// </summary>
    Task<Result<PdfExportAccess>> GetPdfExportAccessAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to savings tracker (Pro+)
    /// </summary>
    Task<Result<bool>> CanUseSavingsTrackerAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to duplicate detection (Premium only)
    /// </summary>
    Task<Result<bool>> CanUseDuplicateDetectionAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all feature access for a user in a single call
    /// </summary>
    Task<Result<FeatureAccessSummary>> GetFeatureAccessSummaryAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific feature is available for a tier (without user lookup)
    /// </summary>
    bool IsFeatureAvailableForTier(TierFeature feature, SubscriptionTier tier);

    /// <summary>
    /// Gets the minimum tier required for a feature
    /// </summary>
    SubscriptionTier GetMinimumTierForFeature(TierFeature feature);
}

/// <summary>
/// PDF export access information
/// </summary>
public record PdfExportAccess(
    bool HasAccess,
    PdfExportLimit Limit,
    int? ExportsThisMonth = null,
    int? ExportsRemaining = null);

/// <summary>
/// Summary of all feature access for a user
/// </summary>
public record FeatureAccessSummary(
    SubscriptionTier CurrentTier,
    // Scanning features
    bool CanUseAiEmailScanning,
    bool CanUseInitial12MonthScan,
    bool CanUseRealTimeScanning,
    // Dashboard features
    bool CanUseAdvancedDashboardFilters,
    bool CanUseCustomCategories,
    // Alert features
    bool CanUse3DayRenewalAlerts,
    bool CanUsePriceChangeAlerts,
    bool CanUseTrialEndingAlerts,
    bool CanUseUnusedSubscriptionAlerts,
    bool CanUseCustomAlertTiming,
    bool CanUseDailyDigest,
    // Insight features
    bool CanUseSpendingByCategory,
    bool CanUseRenewalTimeline,
    bool CanUseSpendingBenchmarks,
    bool CanUseSpendingForecasts,
    // Tool features
    bool CanUseCancellationAssistant,
    PdfExportAccess PdfExportAccess,
    bool CanUseSavingsTracker,
    bool CanUseDuplicateDetection);

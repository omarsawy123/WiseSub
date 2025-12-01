using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Common;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Services;

/// <summary>
/// Service for subscription tier management and limit enforcement
/// Implements three-tier pricing model: Free, Pro, Premium
/// </summary>
public class TierService : ITierService
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailAccountRepository _emailAccountRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILogger<TierService> _logger;

    // Free tier limits
    private const int FreeMaxEmailAccounts = 1;
    private const int FreeMaxSubscriptions = 5;

    // Pro tier limits
    private const int ProMaxEmailAccounts = 3;
    private const int ProMaxSubscriptions = int.MaxValue;

    // Premium tier limits (unlimited)
    private const int PremiumMaxEmailAccounts = int.MaxValue;
    private const int PremiumMaxSubscriptions = int.MaxValue;

    public TierService(
        IUserRepository userRepository,
        IEmailAccountRepository emailAccountRepository,
        ISubscriptionRepository subscriptionRepository,
        ILogger<TierService> logger)
    {
        _userRepository = userRepository;
        _emailAccountRepository = emailAccountRepository;
        _subscriptionRepository = subscriptionRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public TierLimits GetTierLimits(SubscriptionTier tier)
    {
        // Note: SubscriptionTier.Paid (obsolete) has the same value as Pro (1)
        // so it will automatically map to Pro tier limits
        return tier switch
        {
            SubscriptionTier.Free => CreateFreeTierLimits(),
            SubscriptionTier.Pro => CreateProTierLimits(),
            SubscriptionTier.Premium => CreatePremiumTierLimits(),
            _ => throw new ArgumentOutOfRangeException(nameof(tier))
        };
    }


    private static TierLimits CreateFreeTierLimits() => new(
        MaxEmailAccounts: FreeMaxEmailAccounts,
        MaxSubscriptions: FreeMaxSubscriptions,
        HasAiScanning: false,
        HasInitial12MonthScan: false,
        HasRealTimeScanning: false,
        HasAdvancedFilters: false,
        HasCustomCategories: false,
        Has3DayRenewalAlerts: false,
        HasPriceChangeAlerts: false,
        HasTrialEndingAlerts: false,
        HasUnusedSubscriptionAlerts: false,
        HasCustomAlertTiming: false,
        HasDailyDigest: false,
        HasSpendingByCategory: false,
        HasRenewalTimeline: false,
        HasSpendingBenchmarks: false,
        HasSpendingForecasts: false,
        HasCancellationAssistant: false,
        HasCancellationTemplates: false,
        HasPdfExport: false,
        PdfExportLimit: PdfExportLimit.None,
        HasSavingsTracker: false,
        HasDuplicateDetection: false,
        HasUnlimitedHistory: false);

    private static TierLimits CreateProTierLimits() => new(
        MaxEmailAccounts: ProMaxEmailAccounts,
        MaxSubscriptions: ProMaxSubscriptions,
        HasAiScanning: true,
        HasInitial12MonthScan: true,
        HasRealTimeScanning: false,
        HasAdvancedFilters: true,
        HasCustomCategories: false,
        Has3DayRenewalAlerts: true,
        HasPriceChangeAlerts: true,
        HasTrialEndingAlerts: true,
        HasUnusedSubscriptionAlerts: true,
        HasCustomAlertTiming: false,
        HasDailyDigest: false,
        HasSpendingByCategory: true,
        HasRenewalTimeline: true,
        HasSpendingBenchmarks: false,
        HasSpendingForecasts: false,
        HasCancellationAssistant: false,
        HasCancellationTemplates: false,
        HasPdfExport: true,
        PdfExportLimit: PdfExportLimit.Monthly,
        HasSavingsTracker: true,
        HasDuplicateDetection: false,
        HasUnlimitedHistory: true);

    private static TierLimits CreatePremiumTierLimits() => new(
        MaxEmailAccounts: PremiumMaxEmailAccounts,
        MaxSubscriptions: PremiumMaxSubscriptions,
        HasAiScanning: true,
        HasInitial12MonthScan: true,
        HasRealTimeScanning: true,
        HasAdvancedFilters: true,
        HasCustomCategories: true,
        Has3DayRenewalAlerts: true,
        HasPriceChangeAlerts: true,
        HasTrialEndingAlerts: true,
        HasUnusedSubscriptionAlerts: true,
        HasCustomAlertTiming: true,
        HasDailyDigest: true,
        HasSpendingByCategory: true,
        HasRenewalTimeline: true,
        HasSpendingBenchmarks: true,
        HasSpendingForecasts: true,
        HasCancellationAssistant: true,
        HasCancellationTemplates: true,
        HasPdfExport: true,
        PdfExportLimit: PdfExportLimit.Unlimited,
        HasSavingsTracker: true,
        HasDuplicateDetection: true,
        HasUnlimitedHistory: true);

    /// <inheritdoc />
    public async Task<Result<bool>> CanAddEmailAccountAsync(string userId, CancellationToken cancellationToken = default)
    {
        var usageResult = await GetUsageAsync(userId, cancellationToken);
        if (usageResult.IsFailure)
        {
            return Result.Failure<bool>(UserErrors.NotFound);
        }

        var usage = usageResult.Value;
        return Result.Success(!usage.IsAtEmailLimit);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CanAddSubscriptionAsync(string userId, CancellationToken cancellationToken = default)
    {
        var usageResult = await GetUsageAsync(userId, cancellationToken);
        if (usageResult.IsFailure)
        {
            return Result.Failure<bool>(UserErrors.NotFound);
        }

        var usage = usageResult.Value;
        return Result.Success(!usage.IsAtSubscriptionLimit);
    }


    /// <inheritdoc />
    public async Task<Result<bool>> HasFeatureAccessAsync(string userId, TierFeature feature, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<bool>(UserErrors.NotFound);
        }

        var limits = GetTierLimits(user.Tier);
        var hasAccess = feature switch
        {
            // Entry features
            TierFeature.AiScanning => limits.HasAiScanning,
            TierFeature.Initial12MonthScan => limits.HasInitial12MonthScan,
            TierFeature.RealTimeScanning => limits.HasRealTimeScanning,

            // Dashboard features
            TierFeature.AdvancedFilters => limits.HasAdvancedFilters,
            TierFeature.CustomCategories => limits.HasCustomCategories,

            // Alert features
            TierFeature.ThreeDayRenewalAlerts => limits.Has3DayRenewalAlerts,
            TierFeature.PriceChangeAlerts => limits.HasPriceChangeAlerts,
            TierFeature.TrialEndingAlerts => limits.HasTrialEndingAlerts,
            TierFeature.UnusedSubscriptionAlerts => limits.HasUnusedSubscriptionAlerts,
            TierFeature.CustomAlertTiming => limits.HasCustomAlertTiming,
            TierFeature.DailyDigest => limits.HasDailyDigest,

            // Insight features
            TierFeature.SpendingByCategory => limits.HasSpendingByCategory,
            TierFeature.RenewalTimeline => limits.HasRenewalTimeline,
            TierFeature.SpendingBenchmarks => limits.HasSpendingBenchmarks,
            TierFeature.SpendingForecasts => limits.HasSpendingForecasts,

            // Tool features
            TierFeature.CancellationAssistant => limits.HasCancellationAssistant,
            TierFeature.CancellationTemplates => limits.HasCancellationTemplates,
            TierFeature.PdfExport => limits.HasPdfExport,
            TierFeature.SavingsTracker => limits.HasSavingsTracker,
            TierFeature.DuplicateDetection => limits.HasDuplicateDetection,

            // Legacy features (for backward compatibility)
            TierFeature.UnlimitedEmailAccounts => limits.MaxEmailAccounts == int.MaxValue,
            TierFeature.UnlimitedSubscriptions => limits.MaxSubscriptions == int.MaxValue,
            TierFeature.AdvancedInsights => limits.HasSpendingByCategory && limits.HasRenewalTimeline,

            _ => false
        };

        return Result.Success(hasAccess);
    }

    /// <inheritdoc />
    public async Task<Result> UpgradeToTierAsync(string userId, SubscriptionTier targetTier, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        if (user.Tier == targetTier)
        {
            _logger.LogDebug("User {UserId} is already on {Tier} tier", userId, targetTier);
            return Result.Success();
        }

        // Validate upgrade path (can only upgrade, not downgrade via this method)
        if ((int)targetTier < (int)user.Tier)
        {
            _logger.LogWarning("User {UserId} attempted to downgrade via UpgradeToTierAsync", userId);
            return Result.Failure(new Error("InvalidOperation", "Use DowngradeToFreeAsync for downgrades"));
        }

        user.Tier = targetTier;
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User {UserId} upgraded to {Tier} tier", userId, targetTier);
        return Result.Success();
    }

    /// <inheritdoc />
    [Obsolete("Use UpgradeToTierAsync instead")]
    public async Task<Result> UpgradeToPaydAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Legacy method - upgrades to Pro tier for backward compatibility
        return await UpgradeToTierAsync(userId, SubscriptionTier.Pro, cancellationToken);
    }


    /// <inheritdoc />
    public async Task<Result> DowngradeToFreeAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        if (user.Tier == SubscriptionTier.Free)
        {
            _logger.LogDebug("User {UserId} is already on free tier", userId);
            return Result.Success();
        }

        // Downgrade preserves all data but restricts features
        user.Tier = SubscriptionTier.Free;
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User {UserId} downgraded to free tier. Data preserved, features restricted.", userId);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<TierUsage>> GetUsageAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<TierUsage>(UserErrors.NotFound);
        }

        var emailAccounts = await _emailAccountRepository.GetByUserIdAsync(userId, cancellationToken);
        var activeEmailCount = emailAccounts.Count(e => e.IsActive);

        var subscriptions = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        var activeSubscriptionCount = subscriptions.Count(s => 
            s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PendingReview);

        var limits = GetTierLimits(user.Tier);

        var usage = new TierUsage(
            CurrentTier: user.Tier,
            EmailAccountCount: activeEmailCount,
            SubscriptionCount: activeSubscriptionCount,
            Limits: limits,
            IsAtEmailLimit: activeEmailCount >= limits.MaxEmailAccounts,
            IsAtSubscriptionLimit: activeSubscriptionCount >= limits.MaxSubscriptions);

        return Result.Success(usage);
    }

    /// <inheritdoc />
    public async Task<Result> ValidateOperationAsync(string userId, TierOperation operation, CancellationToken cancellationToken = default)
    {
        var usageResult = await GetUsageAsync(userId, cancellationToken);
        if (usageResult.IsFailure)
        {
            return Result.Failure(usageResult.ErrorMessage.Contains("NotFound") 
                ? UserErrors.NotFound 
                : GeneralErrors.UnexpectedError);
        }

        var usage = usageResult.Value;

        return operation switch
        {
            TierOperation.AddEmailAccount when usage.IsAtEmailLimit =>
                Result.Failure(UserErrors.TierLimitExceeded),
            
            TierOperation.AddSubscription when usage.IsAtSubscriptionLimit =>
                Result.Failure(UserErrors.TierLimitExceeded),
            
            TierOperation.ExportPdf when !usage.Limits.HasPdfExport =>
                Result.Failure(UserErrors.TierLimitExceeded),
            
            TierOperation.UseCancellationAssistant when !usage.Limits.HasCancellationAssistant =>
                Result.Failure(UserErrors.TierLimitExceeded),

            TierOperation.UseAiScanning when !usage.Limits.HasAiScanning =>
                Result.Failure(UserErrors.TierLimitExceeded),

            TierOperation.UseRealTimeScanning when !usage.Limits.HasRealTimeScanning =>
                Result.Failure(UserErrors.TierLimitExceeded),

            TierOperation.UseCustomCategories when !usage.Limits.HasCustomCategories =>
                Result.Failure(UserErrors.TierLimitExceeded),

            TierOperation.UseCustomAlertTiming when !usage.Limits.HasCustomAlertTiming =>
                Result.Failure(UserErrors.TierLimitExceeded),

            TierOperation.UseDuplicateDetection when !usage.Limits.HasDuplicateDetection =>
                Result.Failure(UserErrors.TierLimitExceeded),
            
            _ => Result.Success()
        };
    }
}

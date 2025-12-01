using WiseSub.Domain.Common;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Service interface for subscription tier management and limit enforcement
/// </summary>
public interface ITierService
{
    /// <summary>
    /// Gets the tier limits for a specific tier
    /// </summary>
    TierLimits GetTierLimits(SubscriptionTier tier);

    /// <summary>
    /// Checks if a user can add another email account based on their tier
    /// </summary>
    Task<Result<bool>> CanAddEmailAccountAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user can add another subscription based on their tier
    /// </summary>
    Task<Result<bool>> CanAddSubscriptionAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to a specific feature based on their tier
    /// </summary>
    Task<Result<bool>> HasFeatureAccessAsync(string userId, TierFeature feature, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upgrades a user to a paid tier
    /// </summary>
    Task<Result> UpgradeToPaydAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downgrades a user to free tier (preserves data but restricts features)
    /// </summary>
    Task<Result> DowngradeToFreeAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current usage for a user
    /// </summary>
    Task<Result<TierUsage>> GetUsageAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if an operation is allowed based on tier limits
    /// </summary>
    Task<Result> ValidateOperationAsync(string userId, TierOperation operation, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tier limits configuration
/// </summary>
public record TierLimits(
    int MaxEmailAccounts,
    int MaxSubscriptions,
    bool HasCancellationAssistant,
    bool HasPdfExport,
    bool HasUnlimitedHistory);

/// <summary>
/// Current tier usage for a user
/// </summary>
public record TierUsage(
    SubscriptionTier CurrentTier,
    int EmailAccountCount,
    int SubscriptionCount,
    TierLimits Limits,
    bool IsAtEmailLimit,
    bool IsAtSubscriptionLimit);

/// <summary>
/// Features that may be restricted by tier
/// </summary>
public enum TierFeature
{
    CancellationAssistant,
    PdfExport,
    UnlimitedEmailAccounts,
    UnlimitedSubscriptions,
    AdvancedInsights
}

/// <summary>
/// Operations that may be restricted by tier
/// </summary>
public enum TierOperation
{
    AddEmailAccount,
    AddSubscription,
    ExportPdf,
    UseCancellationAssistant
}

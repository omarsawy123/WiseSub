using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Common;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Services;

/// <summary>
/// Service for subscription tier management and limit enforcement
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

    // Paid tier limits (unlimited represented by int.MaxValue)
    private const int PaidMaxEmailAccounts = int.MaxValue;
    private const int PaidMaxSubscriptions = int.MaxValue;

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
        return tier switch
        {
            SubscriptionTier.Free => new TierLimits(
                MaxEmailAccounts: FreeMaxEmailAccounts,
                MaxSubscriptions: FreeMaxSubscriptions,
                HasCancellationAssistant: false,
                HasPdfExport: false,
                HasUnlimitedHistory: false),
            SubscriptionTier.Paid => new TierLimits(
                MaxEmailAccounts: PaidMaxEmailAccounts,
                MaxSubscriptions: PaidMaxSubscriptions,
                HasCancellationAssistant: true,
                HasPdfExport: true,
                HasUnlimitedHistory: true),
            _ => throw new ArgumentOutOfRangeException(nameof(tier))
        };
    }

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
            TierFeature.CancellationAssistant => limits.HasCancellationAssistant,
            TierFeature.PdfExport => limits.HasPdfExport,
            TierFeature.UnlimitedEmailAccounts => limits.MaxEmailAccounts == int.MaxValue,
            TierFeature.UnlimitedSubscriptions => limits.MaxSubscriptions == int.MaxValue,
            TierFeature.AdvancedInsights => user.Tier == SubscriptionTier.Paid,
            _ => false
        };

        return Result.Success(hasAccess);
    }

    /// <inheritdoc />
    public async Task<Result> UpgradeToPaydAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        if (user.Tier == SubscriptionTier.Paid)
        {
            _logger.LogDebug("User {UserId} is already on paid tier", userId);
            return Result.Success();
        }

        user.Tier = SubscriptionTier.Paid;
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User {UserId} upgraded to paid tier", userId);
        return Result.Success();
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
            
            _ => Result.Success()
        };
    }
}

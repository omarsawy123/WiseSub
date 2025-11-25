using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Services;

/// <summary>
/// Service for subscription management with deduplication, status tracking, and billing cycle normalization
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private const double FuzzyMatchThreshold = 0.85;

    public SubscriptionService(ISubscriptionRepository subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository ?? throw new ArgumentNullException(nameof(subscriptionRepository));
    }

    public async Task<Result<Subscription>> CreateOrUpdateAsync(CreateSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return Result.Failure<Subscription>(ValidationErrors.Required);
        
        if (string.IsNullOrWhiteSpace(request.ServiceName))
            return Result.Failure<Subscription>(ValidationErrors.Required);
        
        if (request.Price < 0)
            return Result.Failure<Subscription>(SubscriptionErrors.InvalidPrice);

        // Check for existing duplicate using fuzzy matching
        var existingSubscriptions = await _subscriptionRepository.FindPotentialDuplicatesAsync(
            request.UserId, 
            request.ServiceName, 
            cancellationToken);

        var duplicate = existingSubscriptions
            .FirstOrDefault(s => CalculateSimilarity(s.ServiceName, request.ServiceName) >= FuzzyMatchThreshold);

        if (duplicate != null)
        {
            // Update existing subscription
            return await UpdateExistingSubscription(duplicate, request, cancellationToken);
        }

        // Create new subscription
        var subscription = new Subscription
        {
            UserId = request.UserId,
            EmailAccountId = request.EmailAccountId,
            ServiceName = request.ServiceName,
            Price = request.Price,
            Currency = request.Currency,
            BillingCycle = request.BillingCycle,
            NextRenewalDate = request.NextRenewalDate,
            Category = request.Category ?? string.Empty,
            VendorId = request.VendorId,
            CancellationLink = request.CancellationLink,
            ExtractionConfidence = request.ExtractionConfidence,
            RequiresUserReview = request.ExtractionConfidence < 0.8,
            Status = request.ExtractionConfidence < 0.8 ? SubscriptionStatus.PendingReview : SubscriptionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _subscriptionRepository.AddAsync(subscription, cancellationToken);

        // Add creation history entry
        subscription.History.Add(new SubscriptionHistory
        {
            SubscriptionId = subscription.Id,
            ChangeType = "Created",
            OldValue = string.Empty,
            NewValue = $"Service: {request.ServiceName}, Price: {request.Price} {request.Currency}/{request.BillingCycle}",
            SourceEmailId = request.SourceEmailId,
            ChangedAt = DateTime.UtcNow
        });

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        return Result.Success(subscription);
    }

    private async Task<Result<Subscription>> UpdateExistingSubscription(
        Subscription existing, 
        CreateSubscriptionRequest request, 
        CancellationToken cancellationToken)
    {
        var changes = new List<SubscriptionHistory>();
        
        // Track price changes
        if (existing.Price != request.Price)
        {
            changes.Add(new SubscriptionHistory
            {
                SubscriptionId = existing.Id,
                ChangeType = "PriceChange",
                OldValue = $"{existing.Price} {existing.Currency}",
                NewValue = $"{request.Price} {request.Currency}",
                SourceEmailId = request.SourceEmailId,
                ChangedAt = DateTime.UtcNow
            });
            existing.Price = request.Price;
        }

        // Track billing cycle changes
        if (existing.BillingCycle != request.BillingCycle)
        {
            changes.Add(new SubscriptionHistory
            {
                SubscriptionId = existing.Id,
                ChangeType = "BillingCycleChange",
                OldValue = existing.BillingCycle.ToString(),
                NewValue = request.BillingCycle.ToString(),
                SourceEmailId = request.SourceEmailId,
                ChangedAt = DateTime.UtcNow
            });
            existing.BillingCycle = request.BillingCycle;
        }

        // Update renewal date if provided
        if (request.NextRenewalDate.HasValue)
        {
            existing.NextRenewalDate = request.NextRenewalDate;
        }

        // Update other fields
        existing.Currency = request.Currency;
        existing.LastActivityEmailAt = DateTime.UtcNow;
        existing.UpdatedAt = DateTime.UtcNow;

        // Add history entries
        foreach (var change in changes)
        {
            existing.History.Add(change);
        }

        await _subscriptionRepository.UpdateAsync(existing, cancellationToken);

        return Result.Success(existing);
    }

    public async Task<Result<IEnumerable<Subscription>>> GetUserSubscriptionsAsync(
        string userId,
        SubscriptionStatus? statusFilter = null,
        string? categoryFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<IEnumerable<Subscription>>(ValidationErrors.Required);

        IEnumerable<Subscription> subscriptions;

        if (statusFilter.HasValue)
        {
            subscriptions = await _subscriptionRepository.GetByUserIdAndStatusAsync(
                userId, statusFilter.Value, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(categoryFilter))
        {
            subscriptions = await _subscriptionRepository.GetByCategoryAsync(
                userId, categoryFilter, cancellationToken);
        }
        else
        {
            subscriptions = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        }

        return Result.Success(subscriptions);
    }

    public async Task<Result<Subscription>> GetByIdAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
            return Result.Failure<Subscription>(ValidationErrors.Required);

        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null)
            return Result.Failure<Subscription>(SubscriptionErrors.NotFound);

        return Result.Success(subscription);
    }

    public async Task<Result> UpdateStatusAsync(
        string subscriptionId, 
        SubscriptionStatus newStatus, 
        string? sourceEmailId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
            return Result.Failure(ValidationErrors.Required);

        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null)
            return Result.Failure(SubscriptionErrors.NotFound);

        var oldStatus = subscription.Status;
        
        // Validate status transition
        if (oldStatus == SubscriptionStatus.Cancelled && newStatus == SubscriptionStatus.Active)
        {
            // Allow reactivation - this is a valid business case
        }
        
        if (oldStatus == newStatus)
            return Result.Success(); // No change needed

        // Track the change
        subscription.History.Add(new SubscriptionHistory
        {
            SubscriptionId = subscription.Id,
            ChangeType = "StatusChange",
            OldValue = oldStatus.ToString(),
            NewValue = newStatus.ToString(),
            SourceEmailId = sourceEmailId,
            ChangedAt = DateTime.UtcNow
        });

        subscription.Status = newStatus;
        subscription.UpdatedAt = DateTime.UtcNow;

        if (newStatus == SubscriptionStatus.Cancelled)
        {
            subscription.CancelledAt = DateTime.UtcNow;
        }

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> UpdatePriceAsync(
        string subscriptionId, 
        decimal newPrice, 
        string? sourceEmailId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
            return Result.Failure(ValidationErrors.Required);
        
        if (newPrice < 0)
            return Result.Failure(SubscriptionErrors.InvalidPrice);

        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null)
            return Result.Failure(SubscriptionErrors.NotFound);

        var oldPrice = subscription.Price;
        
        if (oldPrice == newPrice)
            return Result.Success(); // No change needed

        // Track the change
        subscription.History.Add(new SubscriptionHistory
        {
            SubscriptionId = subscription.Id,
            ChangeType = "PriceChange",
            OldValue = $"{oldPrice} {subscription.Currency}",
            NewValue = $"{newPrice} {subscription.Currency}",
            SourceEmailId = sourceEmailId,
            ChangedAt = DateTime.UtcNow
        });

        subscription.Price = newPrice;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IEnumerable<Subscription>>> GetUpcomingRenewalsAsync(
        string userId, 
        int daysAhead = 7,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<IEnumerable<Subscription>>(ValidationErrors.Required);

        var subscriptions = await _subscriptionRepository.GetUpcomingRenewalsAsync(
            userId, daysAhead, cancellationToken);

        return Result.Success(subscriptions);
    }

    public async Task<Result<IEnumerable<Subscription>>> GetPendingReviewAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<IEnumerable<Subscription>>(ValidationErrors.Required);

        var subscriptions = await _subscriptionRepository.GetRequiringReviewAsync(userId, cancellationToken);

        return Result.Success(subscriptions);
    }

    public async Task<Result> ApproveSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
            return Result.Failure(ValidationErrors.Required);

        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null)
            return Result.Failure(SubscriptionErrors.NotFound);

        subscription.RequiresUserReview = false;
        subscription.Status = SubscriptionStatus.Active;
        subscription.UpdatedAt = DateTime.UtcNow;

        subscription.History.Add(new SubscriptionHistory
        {
            SubscriptionId = subscription.Id,
            ChangeType = "Approved",
            OldValue = "PendingReview",
            NewValue = "Active",
            ChangedAt = DateTime.UtcNow
        });

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RejectSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
            return Result.Failure(ValidationErrors.Required);

        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null)
            return Result.Failure(SubscriptionErrors.NotFound);

        subscription.Status = SubscriptionStatus.Archived;
        subscription.RequiresUserReview = false;
        subscription.UpdatedAt = DateTime.UtcNow;

        subscription.History.Add(new SubscriptionHistory
        {
            SubscriptionId = subscription.Id,
            ChangeType = "Rejected",
            OldValue = "PendingReview",
            NewValue = "Archived",
            ChangedAt = DateTime.UtcNow
        });

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<decimal>> GetMonthlySpendingAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<decimal>(ValidationErrors.Required);

        var total = await _subscriptionRepository.GetTotalMonthlySpendingAsync(userId, cancellationToken);

        return Result.Success(total);
    }

    public async Task<Result<Dictionary<string, decimal>>> GetSpendingByCategoryAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<Dictionary<string, decimal>>(ValidationErrors.Required);

        var spending = await _subscriptionRepository.GetSpendingByCategoryAsync(userId, cancellationToken);

        return Result.Success(spending);
    }

    public async Task<Result> ArchiveByEmailAccountAsync(string emailAccountId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(emailAccountId))
            return Result.Failure(ValidationErrors.Required);

        await _subscriptionRepository.ArchiveByEmailAccountAsync(emailAccountId, cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Normalizes billing cycle to monthly equivalent
    /// Annual: Divide by 12, Quarterly: Divide by 3, Weekly: Multiply by 4.33
    /// </summary>
    public decimal NormalizeToMonthly(decimal price, BillingCycle billingCycle)
    {
        return billingCycle switch
        {
            BillingCycle.Annual => price / 12m,
            BillingCycle.Quarterly => price / 3m,
            BillingCycle.Weekly => price * 4.33m,
            BillingCycle.Monthly => price,
            _ => price
        };
    }

    /// <summary>
    /// Calculates similarity between two strings using Levenshtein distance
    /// Returns a value between 0 and 1, where 1 is an exact match
    /// </summary>
    public static double CalculateSimilarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) && string.IsNullOrEmpty(target))
            return 1.0;
        
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            return 0.0;

        // Normalize strings for comparison
        source = source.ToLowerInvariant().Trim();
        target = target.ToLowerInvariant().Trim();

        if (source == target)
            return 1.0;

        int distance = LevenshteinDistance(source, target);
        int maxLength = Math.Max(source.Length, target.Length);
        
        return 1.0 - ((double)distance / maxLength);
    }

    /// <summary>
    /// Computes the Levenshtein distance between two strings
    /// </summary>
    private static int LevenshteinDistance(string source, string target)
    {
        int sourceLength = source.Length;
        int targetLength = target.Length;

        var matrix = new int[sourceLength + 1, targetLength + 1];

        // Initialize first column
        for (int i = 0; i <= sourceLength; i++)
            matrix[i, 0] = i;

        // Initialize first row
        for (int j = 0; j <= targetLength; j++)
            matrix[0, j] = j;

        // Fill matrix
        for (int i = 1; i <= sourceLength; i++)
        {
            for (int j = 1; j <= targetLength; j++)
            {
                int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[sourceLength, targetLength];
    }
}

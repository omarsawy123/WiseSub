using Hangfire;
using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Infrastructure.BackgroundServices.Jobs;

/// <summary>
/// Background job that updates subscription statuses and detects expired subscriptions.
/// Scheduled to run daily at 2 AM to perform maintenance tasks.
/// </summary>
public class SubscriptionUpdateJob
{
    private readonly ILogger<SubscriptionUpdateJob> _logger;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IUserRepository _userRepository;

    public SubscriptionUpdateJob(
        ILogger<SubscriptionUpdateJob> logger,
        ISubscriptionRepository subscriptionRepository,
        IUserRepository userRepository)
    {
        _logger = logger;
        _subscriptionRepository = subscriptionRepository;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Updates subscription statuses for all users.
    /// - Marks past-due renewals as expired
    /// - Updates trial subscriptions that have ended
    /// - Calculates next renewal dates for active subscriptions
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task UpdateAllSubscriptionsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting subscription update job");

        try
        {
            var users = await _userRepository.GetAllAsync(cancellationToken);
            var totalUpdated = 0;

            foreach (var user in users)
            {
                try
                {
                    var updated = await UpdateSubscriptionsForUserAsync(user.Id, cancellationToken);
                    totalUpdated += updated;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update subscriptions for user {UserId}", user.Id);
                }
            }

            _logger.LogInformation("Subscription update completed. Total updated: {UpdateCount}", totalUpdated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in subscription update job");
            throw;
        }
    }

    /// <summary>
    /// Updates subscriptions for a specific user.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task<int> UpdateSubscriptionsForUserAsync(string userId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Updating subscriptions for user {UserId}", userId);

        var updatedCount = 0;
        var subscriptions = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);

        foreach (var subscription in subscriptions)
        {
            try
            {
                var wasUpdated = await UpdateSubscriptionStatusAsync(subscription, cancellationToken);
                if (wasUpdated)
                    updatedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to update subscription {SubscriptionId} for user {UserId}", 
                    subscription.Id, 
                    userId);
            }
        }

        return updatedCount;
    }

    /// <summary>
    /// Updates a single subscription's status based on its current state.
    /// </summary>
    private async Task<bool> UpdateSubscriptionStatusAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var wasUpdated = false;

        // Skip archived or cancelled subscriptions
        if (subscription.Status == SubscriptionStatus.Archived || 
            subscription.Status == SubscriptionStatus.Cancelled)
        {
            return false;
        }

        // Check if trial subscription needs updating (TrialActive status)
        if (subscription.Status == SubscriptionStatus.TrialActive)
        {
            // Trials with past renewal dates become active
            if (subscription.NextRenewalDate.HasValue && subscription.NextRenewalDate.Value <= now)
            {
                _logger.LogInformation(
                    "Trial period ended for subscription {SubscriptionId} ({ServiceName})",
                    subscription.Id,
                    subscription.ServiceName);

                subscription.Status = SubscriptionStatus.Active;

                subscription.History.Add(new SubscriptionHistory
                {
                    SubscriptionId = subscription.Id,
                    ChangeType = "TrialEnded",
                    OldValue = "TrialActive",
                    NewValue = subscription.Status.ToString(),
                    ChangedAt = now
                });

                wasUpdated = true;
            }
        }

        // Check if subscription has expired (past renewal date without payment confirmation)
        if (subscription.Status == SubscriptionStatus.Active && 
            subscription.NextRenewalDate.HasValue)
        {
            var daysPastRenewal = (now - subscription.NextRenewalDate.Value).Days;

            // If more than 7 days past renewal without update, mark as potentially expired
            if (daysPastRenewal > 7)
            {
                _logger.LogInformation(
                    "Subscription {SubscriptionId} ({ServiceName}) is {Days} days past renewal date",
                    subscription.Id,
                    subscription.ServiceName,
                    daysPastRenewal);

                // Don't automatically expire - just flag for review
                subscription.RequiresUserReview = true;
                subscription.History.Add(new SubscriptionHistory
                {
                    SubscriptionId = subscription.Id,
                    ChangeType = "RenewalOverdue",
                    OldValue = subscription.NextRenewalDate.Value.ToString("yyyy-MM-dd"),
                    NewValue = $"Overdue by {daysPastRenewal} days",
                    ChangedAt = now
                });

                wasUpdated = true;
            }
        }

        // Calculate next renewal date if needed
        if (subscription.Status == SubscriptionStatus.Active && 
            subscription.NextRenewalDate.HasValue && 
            subscription.NextRenewalDate.Value <= now)
        {
            var newRenewalDate = CalculateNextRenewalDate(
                subscription.NextRenewalDate.Value, 
                subscription.BillingCycle);

            if (newRenewalDate.HasValue)
            {
                subscription.History.Add(new SubscriptionHistory
                {
                    SubscriptionId = subscription.Id,
                    ChangeType = "RenewalDateAdvanced",
                    OldValue = subscription.NextRenewalDate.Value.ToString("yyyy-MM-dd"),
                    NewValue = newRenewalDate.Value.ToString("yyyy-MM-dd"),
                    ChangedAt = now
                });

                subscription.NextRenewalDate = newRenewalDate;
                wasUpdated = true;
            }
        }

        if (wasUpdated)
        {
            subscription.UpdatedAt = now;
            await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        }

        return wasUpdated;
    }

    /// <summary>
    /// Calculates the next renewal date based on billing cycle.
    /// </summary>
    private static DateTime? CalculateNextRenewalDate(DateTime currentRenewalDate, BillingCycle billingCycle)
    {
        return billingCycle switch
        {
            BillingCycle.Weekly => currentRenewalDate.AddDays(7),
            BillingCycle.Monthly => currentRenewalDate.AddMonths(1),
            BillingCycle.Quarterly => currentRenewalDate.AddMonths(3),
            BillingCycle.Annual => currentRenewalDate.AddYears(1),
            _ => null
        };
    }
}

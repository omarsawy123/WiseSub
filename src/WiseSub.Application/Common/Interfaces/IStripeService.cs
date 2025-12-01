using WiseSub.Domain.Common;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Service interface for Stripe payment processing
/// </summary>
public interface IStripeService
{
    /// <summary>
    /// Creates a Stripe checkout session for tier upgrade
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="targetTier">The tier to upgrade to</param>
    /// <param name="isAnnual">Whether to use annual billing</param>
    /// <param name="successUrl">URL to redirect to on successful payment</param>
    /// <param name="cancelUrl">URL to redirect to on cancelled payment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The checkout session URL</returns>
    Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(
        string userId,
        SubscriptionTier targetTier,
        bool isAnnual,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Stripe billing portal session for subscription management
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="returnUrl">URL to return to after portal session</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The billing portal URL</returns>
    Task<Result<string>> CreateBillingPortalSessionAsync(
        string userId,
        string returnUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles the checkout.session.completed webhook event
    /// </summary>
    Task<Result> HandleCheckoutSessionCompletedAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles the customer.subscription.updated webhook event
    /// </summary>
    Task<Result> HandleSubscriptionUpdatedAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles the customer.subscription.deleted webhook event
    /// </summary>
    Task<Result> HandleSubscriptionDeletedAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the Stripe subscription status for a user
    /// </summary>
    Task<Result<StripeSubscriptionStatus>> GetSubscriptionStatusAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a user's Stripe subscription
    /// </summary>
    Task<Result> CancelSubscriptionAsync(
        string userId,
        bool cancelAtPeriodEnd,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a user's subscription to a different tier (upgrade or downgrade between paid tiers)
    /// Handles proration for mid-cycle changes
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="targetTier">The tier to change to</param>
    /// <param name="isAnnual">Whether to use annual billing</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result<TierChangeResult>> ChangeTierAsync(
        string userId,
        SubscriptionTier targetTier,
        bool isAnnual,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downgrades a user to the free tier
    /// Preserves all user data but restricts features
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="immediate">If true, cancels immediately; if false, cancels at period end</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> DowngradeToFreeAsync(
        string userId,
        bool immediate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of creating a checkout session
/// </summary>
public record CheckoutSessionResult(
    string SessionId,
    string Url);

/// <summary>
/// Result of a tier change operation
/// </summary>
public record TierChangeResult(
    SubscriptionTier PreviousTier,
    SubscriptionTier NewTier,
    bool IsUpgrade,
    decimal? ProratedAmount,
    DateTime? EffectiveDate);

/// <summary>
/// Stripe subscription status information
/// </summary>
public record StripeSubscriptionStatus(
    bool IsActive,
    SubscriptionTier CurrentTier,
    string? StripeSubscriptionId,
    string? StripePriceId,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    bool IsAnnualBilling);

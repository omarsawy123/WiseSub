using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Common;
using WiseSub.Domain.Enums;
using StripeConfig = WiseSub.Application.Common.Configuration.StripeConfiguration;

namespace WiseSub.Infrastructure.Payments;

/// <summary>
/// Stripe payment service implementation
/// </summary>
public class StripeService : IStripeService
{
    private readonly IUserRepository _userRepository;
    private readonly StripeConfig _config;
    private readonly ILogger<StripeService> _logger;

    public StripeService(
        IUserRepository userRepository,
        IOptions<StripeConfig> config,
        ILogger<StripeService> logger)
    {
        _userRepository = userRepository;
        _config = config.Value;
        _logger = logger;

        // Configure Stripe API key
        StripeConfiguration.ApiKey = _config.SecretKey;
    }

    /// <inheritdoc />
    public async Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(
        string userId,
        SubscriptionTier targetTier,
        bool isAnnual,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<CheckoutSessionResult>(UserErrors.NotFound);
        }

        // Get the appropriate price ID
        var priceId = GetPriceId(targetTier, isAnnual);
        if (string.IsNullOrEmpty(priceId))
        {
            _logger.LogError("No price ID configured for tier {Tier}, annual: {IsAnnual}", targetTier, isAnnual);
            return Result.Failure<CheckoutSessionResult>(
                new Error("InvalidTier", $"No price configured for {targetTier} tier"));
        }

        try
        {
            // Create or get Stripe customer
            var customerId = user.StripeCustomerId;
            if (string.IsNullOrEmpty(customerId))
            {
                var customerService = new CustomerService();
                var customer = await customerService.CreateAsync(new CustomerCreateOptions
                {
                    Email = user.Email,
                    Name = user.Name,
                    Metadata = new Dictionary<string, string>
                    {
                        { "userId", userId }
                    }
                }, cancellationToken: cancellationToken);

                customerId = customer.Id;
                user.StripeCustomerId = customerId;
                await _userRepository.UpdateAsync(user, cancellationToken);
            }

            // Create checkout session
            var sessionService = new SessionService();
            var session = await sessionService.CreateAsync(new SessionCreateOptions
            {
                Customer = customerId,
                Mode = "subscription",
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new()
                    {
                        Price = priceId,
                        Quantity = 1
                    }
                },
                SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = cancelUrl,
                Metadata = new Dictionary<string, string>
                {
                    { "userId", userId },
                    { "targetTier", targetTier.ToString() },
                    { "isAnnual", isAnnual.ToString() }
                },
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        { "userId", userId },
                        { "targetTier", targetTier.ToString() }
                    }
                }
            }, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Created checkout session {SessionId} for user {UserId} upgrading to {Tier}",
                session.Id, userId, targetTier);

            return Result.Success(new CheckoutSessionResult(session.Id, session.Url));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating checkout session for user {UserId}", userId);
            return Result.Failure<CheckoutSessionResult>(
                new Error("StripeError", ex.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> CreateBillingPortalSessionAsync(
        string userId,
        string returnUrl,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<string>(UserErrors.NotFound);
        }

        if (string.IsNullOrEmpty(user.StripeCustomerId))
        {
            return Result.Failure<string>(
                new Error("NoSubscription", "User does not have an active subscription"));
        }

        try
        {
            var portalService = new Stripe.BillingPortal.SessionService();
            var session = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = user.StripeCustomerId,
                ReturnUrl = returnUrl
            }, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Created billing portal session for user {UserId}",
                userId);

            return Result.Success(session.Url);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating billing portal session for user {UserId}", userId);
            return Result.Failure<string>(new Error("StripeError", ex.Message));
        }
    }


    /// <inheritdoc />
    public async Task<Result> HandleCheckoutSessionCompletedAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionService = new SessionService();
            var session = await sessionService.GetAsync(sessionId, new SessionGetOptions
            {
                Expand = new List<string> { "subscription" }
            }, cancellationToken: cancellationToken);

            if (!session.Metadata.TryGetValue("userId", out var userId))
            {
                _logger.LogWarning("Checkout session {SessionId} missing userId metadata", sessionId);
                return Result.Failure(new Error("InvalidSession", "Session missing user information"));
            }

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for checkout session {SessionId}", userId, sessionId);
                return Result.Failure(UserErrors.NotFound);
            }

            // Parse target tier from metadata
            if (!session.Metadata.TryGetValue("targetTier", out var tierString) ||
                !Enum.TryParse<SubscriptionTier>(tierString, out var targetTier))
            {
                _logger.LogWarning("Invalid tier in checkout session {SessionId}", sessionId);
                return Result.Failure(new Error("InvalidSession", "Invalid tier information"));
            }

            var isAnnual = session.Metadata.TryGetValue("isAnnual", out var annualString) &&
                           bool.TryParse(annualString, out var annual) && annual;

            // Update user with subscription details
            var subscription = session.Subscription;
            user.StripeSubscriptionId = subscription.Id;
            user.StripePriceId = subscription.Items.Data.FirstOrDefault()?.Price?.Id;
            user.SubscriptionStartDate = subscription.CurrentPeriodStart;
            user.SubscriptionEndDate = subscription.CurrentPeriodEnd;
            user.IsAnnualBilling = isAnnual;
            user.Tier = targetTier;

            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation(
                "User {UserId} upgraded to {Tier} via checkout session {SessionId}",
                userId, targetTier, sessionId);

            return Result.Success();
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error handling checkout session {SessionId}", sessionId);
            return Result.Failure(new Error("StripeError", ex.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result> HandleSubscriptionUpdatedAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriptionService = new SubscriptionService();
            var subscription = await subscriptionService.GetAsync(subscriptionId, 
                cancellationToken: cancellationToken);

            if (!subscription.Metadata.TryGetValue("userId", out var userId))
            {
                _logger.LogWarning("Subscription {SubscriptionId} missing userId metadata", subscriptionId);
                return Result.Failure(new Error("InvalidSubscription", "Subscription missing user information"));
            }

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for subscription {SubscriptionId}", userId, subscriptionId);
                return Result.Failure(UserErrors.NotFound);
            }

            // Update subscription dates
            user.SubscriptionStartDate = subscription.CurrentPeriodStart;
            user.SubscriptionEndDate = subscription.CurrentPeriodEnd;
            user.StripePriceId = subscription.Items.Data.FirstOrDefault()?.Price?.Id;

            // Update tier based on price ID
            var newTier = GetTierFromPriceId(user.StripePriceId);
            if (newTier.HasValue && newTier.Value != user.Tier)
            {
                _logger.LogInformation(
                    "User {UserId} tier changed from {OldTier} to {NewTier}",
                    userId, user.Tier, newTier.Value);
                user.Tier = newTier.Value;
            }

            // Check if subscription is cancelled
            if (subscription.Status == "canceled" || subscription.CancelAtPeriodEnd)
            {
                _logger.LogInformation(
                    "Subscription {SubscriptionId} for user {UserId} is cancelled/cancelling",
                    subscriptionId, userId);
            }

            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation(
                "Updated subscription {SubscriptionId} for user {UserId}",
                subscriptionId, userId);

            return Result.Success();
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error handling subscription update {SubscriptionId}", subscriptionId);
            return Result.Failure(new Error("StripeError", ex.Message));
        }
    }


    /// <inheritdoc />
    public async Task<Result> HandleSubscriptionDeletedAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriptionService = new SubscriptionService();
            var subscription = await subscriptionService.GetAsync(subscriptionId, 
                cancellationToken: cancellationToken);

            if (!subscription.Metadata.TryGetValue("userId", out var userId))
            {
                _logger.LogWarning("Subscription {SubscriptionId} missing userId metadata", subscriptionId);
                return Result.Failure(new Error("InvalidSubscription", "Subscription missing user information"));
            }

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for subscription {SubscriptionId}", userId, subscriptionId);
                return Result.Failure(UserErrors.NotFound);
            }

            // Downgrade to free tier
            user.Tier = SubscriptionTier.Free;
            user.StripeSubscriptionId = null;
            user.StripePriceId = null;
            user.SubscriptionStartDate = null;
            user.SubscriptionEndDate = null;
            user.IsAnnualBilling = false;

            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation(
                "User {UserId} downgraded to Free tier after subscription {SubscriptionId} deleted",
                userId, subscriptionId);

            return Result.Success();
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error handling subscription deletion {SubscriptionId}", subscriptionId);
            return Result.Failure(new Error("StripeError", ex.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result<StripeSubscriptionStatus>> GetSubscriptionStatusAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<StripeSubscriptionStatus>(UserErrors.NotFound);
        }

        if (string.IsNullOrEmpty(user.StripeSubscriptionId))
        {
            return Result.Success(new StripeSubscriptionStatus(
                IsActive: false,
                CurrentTier: user.Tier,
                StripeSubscriptionId: null,
                StripePriceId: null,
                CurrentPeriodStart: null,
                CurrentPeriodEnd: null,
                CancelAtPeriodEnd: false,
                IsAnnualBilling: false));
        }

        try
        {
            var subscriptionService = new SubscriptionService();
            var subscription = await subscriptionService.GetAsync(user.StripeSubscriptionId, 
                cancellationToken: cancellationToken);

            var isActive = subscription.Status == "active" || subscription.Status == "trialing";

            return Result.Success(new StripeSubscriptionStatus(
                IsActive: isActive,
                CurrentTier: user.Tier,
                StripeSubscriptionId: subscription.Id,
                StripePriceId: subscription.Items.Data.FirstOrDefault()?.Price?.Id,
                CurrentPeriodStart: subscription.CurrentPeriodStart,
                CurrentPeriodEnd: subscription.CurrentPeriodEnd,
                CancelAtPeriodEnd: subscription.CancelAtPeriodEnd,
                IsAnnualBilling: user.IsAnnualBilling));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error getting subscription status for user {UserId}", userId);
            return Result.Failure<StripeSubscriptionStatus>(new Error("StripeError", ex.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result> CancelSubscriptionAsync(
        string userId,
        bool cancelAtPeriodEnd,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        if (string.IsNullOrEmpty(user.StripeSubscriptionId))
        {
            return Result.Failure(new Error("NoSubscription", "User does not have an active subscription"));
        }

        try
        {
            var subscriptionService = new SubscriptionService();

            if (cancelAtPeriodEnd)
            {
                // Cancel at end of billing period
                await subscriptionService.UpdateAsync(user.StripeSubscriptionId, new SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = true
                }, cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Subscription {SubscriptionId} for user {UserId} set to cancel at period end",
                    user.StripeSubscriptionId, userId);
            }
            else
            {
                // Cancel immediately
                await subscriptionService.CancelAsync(user.StripeSubscriptionId, 
                    cancellationToken: cancellationToken);

                // Downgrade user immediately
                user.Tier = SubscriptionTier.Free;
                user.StripeSubscriptionId = null;
                user.StripePriceId = null;
                user.SubscriptionStartDate = null;
                user.SubscriptionEndDate = null;
                user.IsAnnualBilling = false;

                await _userRepository.UpdateAsync(user, cancellationToken);

                _logger.LogInformation(
                    "Subscription for user {UserId} cancelled immediately",
                    userId);
            }

            return Result.Success();
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error cancelling subscription for user {UserId}", userId);
            return Result.Failure(new Error("StripeError", ex.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result<TierChangeResult>> ChangeTierAsync(
        string userId,
        SubscriptionTier targetTier,
        bool isAnnual,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<TierChangeResult>(UserErrors.NotFound);
        }

        // Cannot change to Free tier via this method - use DowngradeToFreeAsync
        if (targetTier == SubscriptionTier.Free)
        {
            return Result.Failure<TierChangeResult>(
                new Error("InvalidOperation", "Use DowngradeToFreeAsync to downgrade to free tier"));
        }

        // Get the target price ID
        var newPriceId = GetPriceId(targetTier, isAnnual);
        if (string.IsNullOrEmpty(newPriceId))
        {
            _logger.LogError("No price ID configured for tier {Tier}, annual: {IsAnnual}", targetTier, isAnnual);
            return Result.Failure<TierChangeResult>(
                new Error("InvalidTier", $"No price configured for {targetTier} tier"));
        }

        var previousTier = user.Tier;
        var isUpgrade = (int)targetTier > (int)previousTier;

        // If user doesn't have an active subscription, they need to go through checkout
        if (string.IsNullOrEmpty(user.StripeSubscriptionId))
        {
            return Result.Failure<TierChangeResult>(
                new Error("NoSubscription", "User must have an active subscription to change tiers. Use CreateCheckoutSessionAsync for new subscriptions."));
        }

        try
        {
            var subscriptionService = new SubscriptionService();
            var currentSubscription = await subscriptionService.GetAsync(user.StripeSubscriptionId, 
                cancellationToken: cancellationToken);

            // Get the current subscription item ID
            var currentItemId = currentSubscription.Items.Data.FirstOrDefault()?.Id;
            if (string.IsNullOrEmpty(currentItemId))
            {
                return Result.Failure<TierChangeResult>(
                    new Error("InvalidSubscription", "Could not find subscription item"));
            }

            // Update the subscription with proration
            // Stripe automatically handles proration for mid-cycle changes
            var updateOptions = new SubscriptionUpdateOptions
            {
                Items = new List<SubscriptionItemOptions>
                {
                    new()
                    {
                        Id = currentItemId,
                        Price = newPriceId
                    }
                },
                ProrationBehavior = "create_prorations", // Automatically prorate
                Metadata = new Dictionary<string, string>
                {
                    { "userId", userId },
                    { "targetTier", targetTier.ToString() },
                    { "previousTier", previousTier.ToString() }
                }
            };

            var updatedSubscription = await subscriptionService.UpdateAsync(
                user.StripeSubscriptionId, 
                updateOptions, 
                cancellationToken: cancellationToken);

            // Calculate prorated amount from upcoming invoice
            decimal? proratedAmount = null;
            try
            {
                var invoiceService = new InvoiceService();
                var upcomingInvoice = await invoiceService.UpcomingAsync(new UpcomingInvoiceOptions
                {
                    Customer = user.StripeCustomerId
                }, cancellationToken: cancellationToken);
                
                // Find proration line items
                var prorationItems = upcomingInvoice.Lines.Data
                    .Where(l => l.Proration)
                    .ToList();
                
                if (prorationItems.Any())
                {
                    proratedAmount = prorationItems.Sum(l => l.Amount) / 100m; // Convert from cents
                }
            }
            catch (StripeException ex)
            {
                // Log but don't fail - proration amount is informational
                _logger.LogWarning(ex, "Could not retrieve proration amount for user {UserId}", userId);
            }

            // Update user record
            user.Tier = targetTier;
            user.StripePriceId = newPriceId;
            user.IsAnnualBilling = isAnnual;
            user.SubscriptionStartDate = updatedSubscription.CurrentPeriodStart;
            user.SubscriptionEndDate = updatedSubscription.CurrentPeriodEnd;

            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation(
                "User {UserId} changed tier from {PreviousTier} to {NewTier} (annual: {IsAnnual})",
                userId, previousTier, targetTier, isAnnual);

            return Result.Success(new TierChangeResult(
                PreviousTier: previousTier,
                NewTier: targetTier,
                IsUpgrade: isUpgrade,
                ProratedAmount: proratedAmount,
                EffectiveDate: DateTime.UtcNow));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error changing tier for user {UserId}", userId);
            return Result.Failure<TierChangeResult>(new Error("StripeError", ex.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result> DowngradeToFreeAsync(
        string userId,
        bool immediate,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        // If already on free tier, nothing to do
        if (user.Tier == SubscriptionTier.Free && string.IsNullOrEmpty(user.StripeSubscriptionId))
        {
            _logger.LogDebug("User {UserId} is already on free tier", userId);
            return Result.Success();
        }

        // If user has a Stripe subscription, cancel it
        if (!string.IsNullOrEmpty(user.StripeSubscriptionId))
        {
            try
            {
                var subscriptionService = new SubscriptionService();

                if (immediate)
                {
                    // Cancel immediately
                    await subscriptionService.CancelAsync(user.StripeSubscriptionId, 
                        cancellationToken: cancellationToken);

                    _logger.LogInformation(
                        "Subscription {SubscriptionId} for user {UserId} cancelled immediately",
                        user.StripeSubscriptionId, userId);
                }
                else
                {
                    // Cancel at end of billing period
                    await subscriptionService.UpdateAsync(user.StripeSubscriptionId, new SubscriptionUpdateOptions
                    {
                        CancelAtPeriodEnd = true
                    }, cancellationToken: cancellationToken);

                    _logger.LogInformation(
                        "Subscription {SubscriptionId} for user {UserId} set to cancel at period end",
                        user.StripeSubscriptionId, userId);

                    // Don't downgrade tier yet - will happen via webhook when subscription actually ends
                    return Result.Success();
                }
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe error cancelling subscription for user {UserId}", userId);
                return Result.Failure(new Error("StripeError", ex.Message));
            }
        }

        // Downgrade user to free tier (preserves all data, just restricts features)
        user.Tier = SubscriptionTier.Free;
        user.StripeSubscriptionId = null;
        user.StripePriceId = null;
        user.SubscriptionStartDate = null;
        user.SubscriptionEndDate = null;
        user.IsAnnualBilling = false;
        // Note: StripeCustomerId is preserved for future upgrades

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation(
            "User {UserId} downgraded to free tier. All data preserved, features restricted.",
            userId);

        return Result.Success();
    }

    private string? GetPriceId(SubscriptionTier tier, bool isAnnual)
    {
        return tier switch
        {
            SubscriptionTier.Pro => isAnnual ? _config.PriceIds.ProAnnual : _config.PriceIds.ProMonthly,
            SubscriptionTier.Premium => isAnnual ? _config.PriceIds.PremiumAnnual : _config.PriceIds.PremiumMonthly,
            _ => null
        };
    }

    private SubscriptionTier? GetTierFromPriceId(string? priceId)
    {
        if (string.IsNullOrEmpty(priceId))
            return null;

        if (priceId == _config.PriceIds.ProMonthly || priceId == _config.PriceIds.ProAnnual)
            return SubscriptionTier.Pro;

        if (priceId == _config.PriceIds.PremiumMonthly || priceId == _config.PriceIds.PremiumAnnual)
            return SubscriptionTier.Premium;

        return null;
    }
}

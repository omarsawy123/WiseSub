using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Enums;

namespace WiseSub.API.Controllers;

/// <summary>
/// Controller for pricing, tier management, and Stripe payment operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class PricingController : ControllerBase
{
    private readonly IStripeService _stripeService;
    private readonly ITierService _tierService;
    private readonly ILogger<PricingController> _logger;

    public PricingController(
        IStripeService stripeService,
        ITierService tierService,
        ILogger<PricingController> logger)
    {
        _stripeService = stripeService;
        _tierService = tierService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all available pricing tiers and their features
    /// </summary>
    [HttpGet("tiers")]
    [AllowAnonymous]
    public ActionResult<IEnumerable<TierInfoResponse>> GetTiers()
    {
        var tiers = new[]
        {
            CreateTierInfo(SubscriptionTier.Free),
            CreateTierInfo(SubscriptionTier.Pro),
            CreateTierInfo(SubscriptionTier.Premium)
        };

        return Ok(tiers);
    }

    /// <summary>
    /// Gets the current user's tier and usage information
    /// </summary>
    [HttpGet("current")]
    public async Task<ActionResult<CurrentTierResponse>> GetCurrentTier(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var usageResult = await _tierService.GetUsageAsync(userId, cancellationToken);
        if (usageResult.IsFailure)
        {
            return NotFound(new { error = usageResult.ErrorMessage });
        }

        var usage = usageResult.Value;
        var subscriptionStatus = await _stripeService.GetSubscriptionStatusAsync(userId, cancellationToken);

        return Ok(new CurrentTierResponse
        {
            CurrentTier = usage.CurrentTier.ToString(),
            EmailAccountCount = usage.EmailAccountCount,
            SubscriptionCount = usage.SubscriptionCount,
            MaxEmailAccounts = usage.Limits.MaxEmailAccounts,
            MaxSubscriptions = usage.Limits.MaxSubscriptions,
            IsAtEmailLimit = usage.IsAtEmailLimit,
            IsAtSubscriptionLimit = usage.IsAtSubscriptionLimit,
            StripeSubscriptionId = subscriptionStatus.IsSuccess ? subscriptionStatus.Value.StripeSubscriptionId : null,
            CurrentPeriodEnd = subscriptionStatus.IsSuccess ? subscriptionStatus.Value.CurrentPeriodEnd : null,
            CancelAtPeriodEnd = subscriptionStatus.IsSuccess && subscriptionStatus.Value.CancelAtPeriodEnd,
            IsAnnualBilling = subscriptionStatus.IsSuccess && subscriptionStatus.Value.IsAnnualBilling
        });
    }

    /// <summary>
    /// Creates a Stripe checkout session for upgrading to a paid tier
    /// </summary>
    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponse>> CreateCheckoutSession(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        if (!Enum.TryParse<SubscriptionTier>(request.TargetTier, true, out var targetTier))
        {
            return BadRequest(new { error = "Invalid target tier" });
        }

        if (targetTier == SubscriptionTier.Free)
        {
            return BadRequest(new { error = "Cannot checkout for free tier. Use downgrade endpoint instead." });
        }

        var result = await _stripeService.CreateCheckoutSessionAsync(
            userId,
            targetTier,
            request.IsAnnual,
            request.SuccessUrl,
            request.CancelUrl,
            cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to create checkout session for user {UserId}: {Error}", userId, result.ErrorMessage);
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new CheckoutResponse
        {
            SessionId = result.Value.SessionId,
            Url = result.Value.Url
        });
    }

    /// <summary>
    /// Creates a Stripe billing portal session for managing subscription
    /// </summary>
    [HttpPost("portal")]
    public async Task<ActionResult<PortalResponse>> CreateBillingPortalSession(
        [FromBody] PortalRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _stripeService.CreateBillingPortalSessionAsync(
            userId,
            request.ReturnUrl,
            cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to create billing portal session for user {UserId}: {Error}", userId, result.ErrorMessage);
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new PortalResponse { Url = result.Value });
    }

    /// <summary>
    /// Upgrades or changes the user's subscription tier (for existing subscribers)
    /// Handles proration for mid-cycle changes
    /// </summary>
    [HttpPost("upgrade")]
    public async Task<ActionResult<TierChangeResponse>> UpgradeTier(
        [FromBody] TierChangeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        if (!Enum.TryParse<SubscriptionTier>(request.TargetTier, true, out var targetTier))
        {
            return BadRequest(new { error = "Invalid target tier" });
        }

        if (targetTier == SubscriptionTier.Free)
        {
            return BadRequest(new { error = "Use the downgrade endpoint to switch to free tier" });
        }

        var result = await _stripeService.ChangeTierAsync(
            userId,
            targetTier,
            request.IsAnnual,
            cancellationToken);

        if (result.IsFailure)
        {
            // If user doesn't have a subscription, they need to go through checkout
            if (result.ErrorMessage.Contains("NoSubscription"))
            {
                return BadRequest(new { 
                    error = "No active subscription found. Use the checkout endpoint to subscribe.",
                    requiresCheckout = true
                });
            }

            _logger.LogWarning("Failed to change tier for user {UserId}: {Error}", userId, result.ErrorMessage);
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new TierChangeResponse
        {
            PreviousTier = result.Value.PreviousTier.ToString(),
            NewTier = result.Value.NewTier.ToString(),
            IsUpgrade = result.Value.IsUpgrade,
            ProratedAmount = result.Value.ProratedAmount,
            EffectiveDate = result.Value.EffectiveDate
        });
    }

    /// <summary>
    /// Downgrades the user to the free tier
    /// Preserves all user data but restricts features
    /// </summary>
    [HttpPost("downgrade")]
    public async Task<ActionResult<DowngradeResponse>> DowngradeToFree(
        [FromBody] DowngradeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _stripeService.DowngradeToFreeAsync(
            userId,
            request.Immediate,
            cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to downgrade user {UserId}: {Error}", userId, result.ErrorMessage);
            return BadRequest(new { error = result.ErrorMessage });
        }

        var message = request.Immediate
            ? "Successfully downgraded to free tier. Your data has been preserved."
            : "Your subscription will be cancelled at the end of the current billing period. Your data will be preserved.";

        return Ok(new DowngradeResponse
        {
            Success = true,
            Message = message,
            Immediate = request.Immediate
        });
    }

    /// <summary>
    /// Handles Stripe webhook events
    /// </summary>
    [HttpPost("webhooks")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleWebhook(CancellationToken cancellationToken)
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync(cancellationToken);
        var stripeSignature = Request.Headers["Stripe-Signature"];

        // Note: In production, verify the webhook signature using the webhook secret
        // For now, we'll process the event directly

        try
        {
            var stripeEvent = Stripe.EventUtility.ParseEvent(json);

            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                    var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                    if (session != null)
                    {
                        await _stripeService.HandleCheckoutSessionCompletedAsync(session.Id, cancellationToken);
                    }
                    break;

                case "customer.subscription.updated":
                    var updatedSubscription = stripeEvent.Data.Object as Stripe.Subscription;
                    if (updatedSubscription != null)
                    {
                        await _stripeService.HandleSubscriptionUpdatedAsync(updatedSubscription.Id, cancellationToken);
                    }
                    break;

                case "customer.subscription.deleted":
                    var deletedSubscription = stripeEvent.Data.Object as Stripe.Subscription;
                    if (deletedSubscription != null)
                    {
                        await _stripeService.HandleSubscriptionDeletedAsync(deletedSubscription.Id, cancellationToken);
                    }
                    break;

                default:
                    _logger.LogDebug("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                    break;
            }

            return Ok();
        }
        catch (Stripe.StripeException ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook");
            return BadRequest();
        }
    }

    private string? GetUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }

    private TierInfoResponse CreateTierInfo(SubscriptionTier tier)
    {
        var limits = _tierService.GetTierLimits(tier);
        
        return new TierInfoResponse
        {
            Name = tier.ToString(),
            MaxEmailAccounts = limits.MaxEmailAccounts == int.MaxValue ? null : limits.MaxEmailAccounts,
            MaxSubscriptions = limits.MaxSubscriptions == int.MaxValue ? null : limits.MaxSubscriptions,
            Features = GetFeaturesForTier(limits),
            MonthlyPrice = GetMonthlyPrice(tier),
            AnnualPrice = GetAnnualPrice(tier)
        };
    }

    private static List<string> GetFeaturesForTier(TierLimits limits)
    {
        var features = new List<string>();

        if (limits.HasAiScanning) features.Add("AI Email Scanning");
        if (limits.HasInitial12MonthScan) features.Add("12-Month Historical Scan");
        if (limits.HasRealTimeScanning) features.Add("Real-time Email Scanning");
        if (limits.HasAdvancedFilters) features.Add("Advanced Dashboard Filters");
        if (limits.HasCustomCategories) features.Add("Custom Categories");
        if (limits.Has3DayRenewalAlerts) features.Add("3-Day Renewal Alerts");
        if (limits.HasPriceChangeAlerts) features.Add("Price Change Alerts");
        if (limits.HasTrialEndingAlerts) features.Add("Trial Ending Alerts");
        if (limits.HasUnusedSubscriptionAlerts) features.Add("Unused Subscription Alerts");
        if (limits.HasCustomAlertTiming) features.Add("Custom Alert Timing");
        if (limits.HasDailyDigest) features.Add("Daily Digest Option");
        if (limits.HasSpendingByCategory) features.Add("Spending by Category");
        if (limits.HasRenewalTimeline) features.Add("Renewal Timeline");
        if (limits.HasSpendingBenchmarks) features.Add("Spending Benchmarks");
        if (limits.HasSpendingForecasts) features.Add("Spending Forecasts");
        if (limits.HasCancellationAssistant) features.Add("Cancellation Assistant");
        if (limits.HasPdfExport) features.Add($"PDF Export ({limits.PdfExportLimit})");
        if (limits.HasSavingsTracker) features.Add("Savings Tracker");
        if (limits.HasDuplicateDetection) features.Add("Duplicate Detection");

        return features;
    }

    private static decimal? GetMonthlyPrice(SubscriptionTier tier)
    {
        return tier switch
        {
            SubscriptionTier.Free => 0,
            SubscriptionTier.Pro => 9.99m,
            SubscriptionTier.Premium => 19.99m,
            _ => null
        };
    }

    private static decimal? GetAnnualPrice(SubscriptionTier tier)
    {
        return tier switch
        {
            SubscriptionTier.Free => 0,
            SubscriptionTier.Pro => 99.99m,  // ~17% discount
            SubscriptionTier.Premium => 199.99m,  // ~17% discount
            _ => null
        };
    }
}

#region Request/Response DTOs

public class CheckoutRequest
{
    public string TargetTier { get; set; } = string.Empty;
    public bool IsAnnual { get; set; }
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
}

public class CheckoutResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class PortalRequest
{
    public string ReturnUrl { get; set; } = string.Empty;
}

public class PortalResponse
{
    public string Url { get; set; } = string.Empty;
}

public class TierChangeRequest
{
    public string TargetTier { get; set; } = string.Empty;
    public bool IsAnnual { get; set; }
}

public class TierChangeResponse
{
    public string PreviousTier { get; set; } = string.Empty;
    public string NewTier { get; set; } = string.Empty;
    public bool IsUpgrade { get; set; }
    public decimal? ProratedAmount { get; set; }
    public DateTime? EffectiveDate { get; set; }
}

public class DowngradeRequest
{
    /// <summary>
    /// If true, cancels immediately. If false, cancels at end of billing period.
    /// </summary>
    public bool Immediate { get; set; }
}

public class DowngradeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Immediate { get; set; }
}

public class TierInfoResponse
{
    public string Name { get; set; } = string.Empty;
    public int? MaxEmailAccounts { get; set; }
    public int? MaxSubscriptions { get; set; }
    public List<string> Features { get; set; } = new();
    public decimal? MonthlyPrice { get; set; }
    public decimal? AnnualPrice { get; set; }
}

public class CurrentTierResponse
{
    public string CurrentTier { get; set; } = string.Empty;
    public int EmailAccountCount { get; set; }
    public int SubscriptionCount { get; set; }
    public int MaxEmailAccounts { get; set; }
    public int MaxSubscriptions { get; set; }
    public bool IsAtEmailLimit { get; set; }
    public bool IsAtSubscriptionLimit { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public bool IsAnnualBilling { get; set; }
}

#endregion
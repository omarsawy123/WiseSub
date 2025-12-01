namespace WiseSub.Application.Common.Configuration;

/// <summary>
/// Configuration for Stripe payment integration
/// </summary>
public class StripeConfiguration
{
    public const string SectionName = "Stripe";

    /// <summary>
    /// Stripe secret API key
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Stripe publishable API key (for frontend)
    /// </summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>
    /// Webhook signing secret for verifying webhook events
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Price IDs for different tiers and billing cycles
    /// </summary>
    public StripePriceIds PriceIds { get; set; } = new();
}

/// <summary>
/// Stripe price IDs for subscription tiers
/// </summary>
public class StripePriceIds
{
    /// <summary>
    /// Pro tier monthly price ID
    /// </summary>
    public string ProMonthly { get; set; } = string.Empty;

    /// <summary>
    /// Pro tier annual price ID
    /// </summary>
    public string ProAnnual { get; set; } = string.Empty;

    /// <summary>
    /// Premium tier monthly price ID
    /// </summary>
    public string PremiumMonthly { get; set; } = string.Empty;

    /// <summary>
    /// Premium tier annual price ID
    /// </summary>
    public string PremiumAnnual { get; set; } = string.Empty;
}

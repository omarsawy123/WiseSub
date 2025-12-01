namespace WiseSub.Domain.Enums;

/// <summary>
/// Subscription tiers for the WiseSub platform
/// </summary>
public enum SubscriptionTier
{
    /// <summary>
    /// Free tier: 1 email account, 5 subscriptions, no AI scanning
    /// </summary>
    Free = 0,

    /// <summary>
    /// Pro tier: 3 email accounts, unlimited subscriptions, AI scanning enabled
    /// </summary>
    Pro = 1,

    /// <summary>
    /// Premium tier: Unlimited email accounts, unlimited subscriptions, all features
    /// </summary>
    Premium = 2,

    /// <summary>
    /// Legacy paid tier - maps to Pro for backward compatibility
    /// </summary>
    [Obsolete("Use Pro or Premium instead. Kept for backward compatibility.")]
    Paid = 1
}

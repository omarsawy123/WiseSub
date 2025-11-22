namespace WiseSub.Application.Common.Models;

/// <summary>
/// Priority levels for email processing
/// </summary>
public enum EmailProcessingPriority
{
    /// <summary>
    /// Low priority - updates to existing subscriptions
    /// </summary>
    Low = 0,

    /// <summary>
    /// Normal priority - general subscription emails
    /// </summary>
    Normal = 1,

    /// <summary>
    /// High priority - new subscription detection
    /// </summary>
    High = 2
}

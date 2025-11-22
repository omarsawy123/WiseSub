namespace WiseSub.Application.Common.Models;

/// <summary>
/// Result of email classification
/// </summary>
public class ClassificationResult
{
    /// <summary>
    /// Whether the email is subscription-related
    /// </summary>
    public bool IsSubscriptionRelated { get; set; }

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Type of subscription email (if classified as subscription-related)
    /// </summary>
    public string? EmailType { get; set; }

    /// <summary>
    /// Reason for classification
    /// </summary>
    public string? Reason { get; set; }
}

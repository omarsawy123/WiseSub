using WiseSub.Domain.Enums;

namespace WiseSub.Application.Common.Models;

/// <summary>
/// Result of subscription data extraction from email
/// </summary>
public class ExtractionResult
{
    /// <summary>
    /// Name of the service/subscription
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Price of the subscription
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Currency code (e.g., USD, EUR, GBP)
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Billing cycle
    /// </summary>
    public BillingCycle BillingCycle { get; set; }

    /// <summary>
    /// Next renewal date (if available)
    /// </summary>
    public DateTime? NextRenewalDate { get; set; }

    /// <summary>
    /// Category of the subscription
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Cancellation link (if available)
    /// </summary>
    public string? CancellationLink { get; set; }

    /// <summary>
    /// Overall confidence score (0.0 to 1.0)
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Whether this extraction requires user review
    /// </summary>
    public bool RequiresUserReview { get; set; }

    /// <summary>
    /// Individual field confidence scores
    /// </summary>
    public Dictionary<string, double> FieldConfidences { get; set; } = new();

    /// <summary>
    /// Any warnings or notes about the extraction
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

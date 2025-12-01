namespace WiseSub.Application.Common.Models;

/// <summary>
/// Represents a renewal event on the timeline
/// </summary>
public class RenewalEvent
{
    /// <summary>
    /// Subscription ID
    /// </summary>
    public string SubscriptionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Service/subscription name
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Renewal date
    /// </summary>
    public DateTime RenewalDate { get; set; }
    
    /// <summary>
    /// Amount to be charged on renewal
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Currency code (e.g., USD, EUR)
    /// </summary>
    public string Currency { get; set; } = "USD";
    
    /// <summary>
    /// Billing cycle description
    /// </summary>
    public string BillingCycle { get; set; } = string.Empty;
    
    /// <summary>
    /// Category of the subscription
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Vendor logo URL if available
    /// </summary>
    public string? VendorLogoUrl { get; set; }
    
    /// <summary>
    /// Whether this renewal is within the danger zone (next 7 days)
    /// </summary>
    public bool IsInDangerZone { get; set; }
    
    /// <summary>
    /// Whether this is a high-value renewal (top 20% by amount)
    /// </summary>
    public bool IsHighValue { get; set; }
    
    /// <summary>
    /// Month name for grouping (e.g., "January 2025")
    /// </summary>
    public string MonthGroup { get; set; } = string.Empty;
}

/// <summary>
/// Timeline data with renewals grouped by month
/// </summary>
public class RenewalTimeline
{
    /// <summary>
    /// All renewal events in chronological order
    /// </summary>
    public IEnumerable<RenewalEvent> Events { get; set; } = Enumerable.Empty<RenewalEvent>();
    
    /// <summary>
    /// Monthly summary with total amounts
    /// </summary>
    public IEnumerable<MonthlyRenewalSummary> MonthlySummaries { get; set; } = Enumerable.Empty<MonthlyRenewalSummary>();
    
    /// <summary>
    /// Total amount across all renewals in the timeline
    /// </summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// Number of months covered by the timeline
    /// </summary>
    public int MonthsCovered { get; set; }
}

/// <summary>
/// Monthly summary for timeline
/// </summary>
public class MonthlyRenewalSummary
{
    /// <summary>
    /// Month name (e.g., "January 2025")
    /// </summary>
    public string Month { get; set; } = string.Empty;
    
    /// <summary>
    /// Year and month for sorting (e.g., 202501)
    /// </summary>
    public int YearMonth { get; set; }
    
    /// <summary>
    /// Number of renewals in this month
    /// </summary>
    public int RenewalCount { get; set; }
    
    /// <summary>
    /// Total amount for renewals in this month
    /// </summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// Whether this is a high-spend month (above average)
    /// </summary>
    public bool IsHighSpend { get; set; }
}

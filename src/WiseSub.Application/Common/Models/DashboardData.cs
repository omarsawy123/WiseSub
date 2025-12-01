using WiseSub.Domain.Entities;

namespace WiseSub.Application.Common.Models;

/// <summary>
/// Dashboard data containing subscription overview and spending summary
/// </summary>
public class DashboardData
{
    /// <summary>
    /// All active subscriptions for the user
    /// </summary>
    public IEnumerable<SubscriptionSummary> ActiveSubscriptions { get; set; } = Enumerable.Empty<SubscriptionSummary>();
    
    /// <summary>
    /// Total monthly spending across all active subscriptions (normalized)
    /// </summary>
    public decimal TotalMonthlySpend { get; set; }
    
    /// <summary>
    /// Spending breakdown by category (category name -> monthly amount)
    /// </summary>
    public Dictionary<string, decimal> SpendingByCategory { get; set; } = new();
    
    /// <summary>
    /// Subscriptions with renewals in the next 30 days
    /// </summary>
    public IEnumerable<SubscriptionSummary> UpcomingRenewals { get; set; } = Enumerable.Empty<SubscriptionSummary>();
    
    /// <summary>
    /// Total count of all subscriptions (all statuses)
    /// </summary>
    public int TotalSubscriptionCount { get; set; }
    
    /// <summary>
    /// Count of active subscriptions only
    /// </summary>
    public int ActiveSubscriptionCount { get; set; }
    
    /// <summary>
    /// Subscriptions requiring user review (low confidence extractions)
    /// </summary>
    public IEnumerable<SubscriptionSummary> RequiringReview { get; set; } = Enumerable.Empty<SubscriptionSummary>();
}

/// <summary>
/// Simplified subscription data for dashboard display
/// </summary>
public class SubscriptionSummary
{
    public string Id { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public string BillingCycle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? NextRenewalDate { get; set; }
    public int? DaysUntilRenewal { get; set; }
    public bool IsInDangerZone { get; set; } // Renewal within 7 days
    public string? VendorLogoUrl { get; set; }
    public string? VendorWebsiteUrl { get; set; }
    public string EmailAccountId { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
}

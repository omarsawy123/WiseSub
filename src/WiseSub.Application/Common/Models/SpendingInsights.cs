namespace WiseSub.Application.Common.Models;

/// <summary>
/// Detailed spending insights and analytics
/// </summary>
public class SpendingInsights
{
    /// <summary>
    /// Total monthly spending across all subscriptions (normalized)
    /// </summary>
    public decimal TotalMonthlySpend { get; set; }
    
    /// <summary>
    /// Projected yearly spending (monthly * 12)
    /// </summary>
    public decimal ProjectedYearlySpend { get; set; }
    
    /// <summary>
    /// Spending breakdown by category with percentages
    /// </summary>
    public IEnumerable<CategorySpending> SpendingByCategory { get; set; } = Enumerable.Empty<CategorySpending>();
    
    /// <summary>
    /// Weekly renewal concentration analysis
    /// </summary>
    public IEnumerable<WeeklySpending> WeeklyRenewalConcentration { get; set; } = Enumerable.Empty<WeeklySpending>();
    
    /// <summary>
    /// Most expensive subscription
    /// </summary>
    public string? MostExpensiveSubscription { get; set; }
    
    /// <summary>
    /// Monthly cost of most expensive subscription
    /// </summary>
    public decimal MostExpensiveAmount { get; set; }
    
    /// <summary>
    /// Average monthly cost per subscription
    /// </summary>
    public decimal AverageMonthlyPerSubscription { get; set; }
    
    /// <summary>
    /// Number of active subscriptions used in calculation
    /// </summary>
    public int ActiveSubscriptionCount { get; set; }
}

/// <summary>
/// Category spending breakdown
/// </summary>
public class CategorySpending
{
    public string Category { get; set; } = string.Empty;
    public decimal MonthlyAmount { get; set; }
    public decimal Percentage { get; set; }
    public int SubscriptionCount { get; set; }
}

/// <summary>
/// Weekly spending concentration for renewal analysis
/// </summary>
public class WeeklySpending
{
    /// <summary>
    /// Week of the month (1-5)
    /// </summary>
    public int WeekOfMonth { get; set; }
    
    /// <summary>
    /// Total renewals occurring in this week
    /// </summary>
    public int RenewalCount { get; set; }
    
    /// <summary>
    /// Total amount renewing in this week
    /// </summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// Whether this week has high concentration (>40% of renewals)
    /// </summary>
    public bool IsHighConcentration { get; set; }
}

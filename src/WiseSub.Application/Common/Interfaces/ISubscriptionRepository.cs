using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Repository interface for Subscription entity operations with filtering and aggregation
/// </summary>
public interface ISubscriptionRepository : IRepository<Subscription>
{
    /// <summary>
    /// Gets all subscriptions for a specific user
    /// </summary>
    Task<IEnumerable<Subscription>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets subscriptions by user ID with specific status
    /// </summary>
    Task<IEnumerable<Subscription>> GetByUserIdAndStatusAsync(string userId, SubscriptionStatus status, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets subscriptions for a specific email account
    /// </summary>
    Task<IEnumerable<Subscription>> GetByEmailAccountIdAsync(string emailAccountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets subscriptions with upcoming renewals within specified days
    /// </summary>
    Task<IEnumerable<Subscription>> GetUpcomingRenewalsAsync(string userId, int daysAhead, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets subscriptions by category
    /// </summary>
    Task<IEnumerable<Subscription>> GetByCategoryAsync(string userId, string category, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets subscriptions with vendor metadata included
    /// </summary>
    Task<IEnumerable<Subscription>> GetWithVendorAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the total monthly spending for a user (normalized to monthly)
    /// </summary>
    Task<decimal> GetTotalMonthlySpendingAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets spending breakdown by category for a user
    /// </summary>
    Task<Dictionary<string, decimal>> GetSpendingByCategoryAsync(string userId, CancellationToken cancellationToken = default);
}

public class SubscriptionStats
{
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int CancelledCount { get; set; }
    public int TrialCount { get; set; }
    public decimal MonthlyTotal { get; set; }
    public decimal YearlyTotal { get; set; }
}

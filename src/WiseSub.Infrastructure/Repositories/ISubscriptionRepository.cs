using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Infrastructure.Repositories;

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
    /// Gets subscriptions that require user review
    /// </summary>
    Task<IEnumerable<Subscription>> GetRequiringReviewAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets total monthly spending for a user (normalized)
    /// </summary>
    Task<decimal> GetTotalMonthlySpendingAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets spending breakdown by category
    /// </summary>
    Task<Dictionary<string, decimal>> GetSpendingByCategoryAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Finds duplicate subscriptions by service name and email account
    /// </summary>
    Task<Subscription?> FindDuplicateAsync(string serviceName, string emailAccountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Archives subscriptions for a disconnected email account
    /// </summary>
    Task ArchiveByEmailAccountAsync(string emailAccountId, CancellationToken cancellationToken = default);
}

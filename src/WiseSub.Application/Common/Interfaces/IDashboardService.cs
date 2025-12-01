using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Service for aggregating dashboard data, spending insights, and renewal timeline
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Gets aggregated dashboard data for a user including active subscriptions,
    /// spending summary, upcoming renewals, and items requiring review
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dashboard data with subscription overview</returns>
    Task<Result<DashboardData>> GetDashboardDataAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets detailed spending insights including category breakdown,
    /// weekly concentration analysis, and spending statistics
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Spending insights with analytics</returns>
    Task<Result<SpendingInsights>> GetSpendingInsightsAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets renewal timeline showing upcoming renewals for the specified number of months
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <param name="months">Number of months to include (default 12)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Renewal timeline with monthly summaries</returns>
    Task<Result<RenewalTimeline>> GetRenewalTimelineAsync(string userId, int months = 12, CancellationToken cancellationToken = default);
}

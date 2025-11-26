using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Repository interface for Alert entity operations with scheduling queries
/// </summary>
public interface IAlertRepository : IRepository<Alert>
{
    /// <summary>
    /// Gets all alerts for a specific user
    /// </summary>
    Task<IEnumerable<Alert>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets alerts by subscription ID
    /// </summary>
    Task<IEnumerable<Alert>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a specific alert by subscription ID and type (for deduplication)
    /// </summary>
    Task<Alert?> GetBySubscriptionAndTypeAsync(string subscriptionId, AlertType type, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets pending alerts scheduled before or at the specified time
    /// </summary>
    Task<IEnumerable<Alert>> GetPendingAlertsAsync(DateTime scheduledBefore, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets alerts by status
    /// </summary>
    Task<IEnumerable<Alert>> GetByStatusAsync(AlertStatus status, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets alerts by type for a user
    /// </summary>
    Task<IEnumerable<Alert>> GetByTypeAsync(string userId, AlertType type, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks an alert as sent
    /// </summary>
    Task MarkAsSentAsync(string alertId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks an alert as failed
    /// </summary>
    Task MarkAsFailedAsync(string alertId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets alerts scheduled for today
    /// </summary>
    Task<IEnumerable<Alert>> GetTodaysAlertsAsync(string userId, CancellationToken cancellationToken = default);
}

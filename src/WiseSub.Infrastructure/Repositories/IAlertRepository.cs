using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Infrastructure.Repositories;

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
    /// Marks an alert as failed and increments retry count
    /// </summary>
    Task MarkAsFailedAsync(string alertId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if an alert already exists for a subscription and type
    /// </summary>
    Task<bool> ExistsAsync(string subscriptionId, AlertType type, DateTime scheduledFor, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes old sent alerts (cleanup)
    /// </summary>
    Task DeleteOldSentAlertsAsync(DateTime olderThan, CancellationToken cancellationToken = default);
}

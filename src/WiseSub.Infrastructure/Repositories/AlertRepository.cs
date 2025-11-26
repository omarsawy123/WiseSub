using Microsoft.EntityFrameworkCore;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;
using WiseSub.Infrastructure.Data;

namespace WiseSub.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Alert entity with scheduling queries
/// </summary>
public class AlertRepository : Repository<Alert>, IAlertRepository
{
    public AlertRepository(WiseSubDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Alert>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(a => a.UserId == userId)
            .Include(a => a.Subscription)
            .OrderByDescending(a => a.ScheduledFor)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Alert>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(a => a.SubscriptionId == subscriptionId)
            .OrderByDescending(a => a.ScheduledFor)
            .ToListAsync(cancellationToken);
    }

    public async Task<Alert?> GetBySubscriptionAndTypeAsync(string subscriptionId, AlertType type, CancellationToken cancellationToken = default)
    {
        // Check for existing alert within the last 30 days to avoid duplicates
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        return await _dbSet
            .Where(a => a.SubscriptionId == subscriptionId 
                && a.Type == type 
                && a.ScheduledFor >= thirtyDaysAgo)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<Alert>> GetPendingAlertsAsync(DateTime scheduledBefore, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(a => a.Status == AlertStatus.Pending && a.ScheduledFor <= scheduledBefore)
            .Include(a => a.User)
            .Include(a => a.Subscription)
            .OrderBy(a => a.ScheduledFor)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Alert>> GetByStatusAsync(AlertStatus status, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(a => a.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Alert>> GetByTypeAsync(string userId, AlertType type, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(a => a.UserId == userId && a.Type == type)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsSentAsync(string alertId, CancellationToken cancellationToken = default)
    {
        var alert = await GetByIdAsync(alertId, cancellationToken);
        if (alert != null)
        {
            alert.Status = AlertStatus.Sent;
            alert.SentAt = DateTime.UtcNow;
            await UpdateAsync(alert, cancellationToken);
        }
    }

    public async Task MarkAsFailedAsync(string alertId, CancellationToken cancellationToken = default)
    {
        var alert = await GetByIdAsync(alertId, cancellationToken);
        if (alert != null)
        {
            alert.Status = AlertStatus.Failed;
            alert.RetryCount++;
            await UpdateAsync(alert, cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(string subscriptionId, AlertType type, DateTime scheduledFor, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AnyAsync(a => 
                a.SubscriptionId == subscriptionId 
                && a.Type == type 
                && a.ScheduledFor.Date == scheduledFor.Date
                && a.Status != AlertStatus.Failed,
                cancellationToken);
    }

    public async Task DeleteOldSentAlertsAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        var oldAlerts = await _dbSet
            .Where(a => a.Status == AlertStatus.Sent && a.SentAt < olderThan)
            .ToListAsync(cancellationToken);

        _dbSet.RemoveRange(oldAlerts);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<IEnumerable<Alert>> GetTodaysAlertsAsync(string userId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

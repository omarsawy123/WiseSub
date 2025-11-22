using Microsoft.EntityFrameworkCore;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;
using WiseSub.Infrastructure.Data;

namespace WiseSub.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Subscription entity with filtering and aggregation
/// </summary>
public class SubscriptionRepository : Repository<Subscription>, ISubscriptionRepository
{
    public SubscriptionRepository(WiseSubDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Subscription>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(s => s.UserId == userId)
            .Include(s => s.Vendor)
            .Include(s => s.EmailAccount)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Subscription>> GetByUserIdAndStatusAsync(string userId, SubscriptionStatus status, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(s => s.UserId == userId && s.Status == status)
            .Include(s => s.Vendor)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Subscription>> GetByEmailAccountIdAsync(string emailAccountId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(s => s.EmailAccountId == emailAccountId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Subscription>> GetUpcomingRenewalsAsync(string userId, int daysAhead, CancellationToken cancellationToken = default)
    {
        var targetDate = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Where(s => s.UserId == userId 
                && s.Status == SubscriptionStatus.Active 
                && s.NextRenewalDate.HasValue
                && s.NextRenewalDate.Value <= targetDate
                && s.NextRenewalDate.Value >= DateTime.UtcNow)
            .Include(s => s.Vendor)
            .OrderBy(s => s.NextRenewalDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Subscription>> GetByCategoryAsync(string userId, string category, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(s => s.UserId == userId && s.Category == category)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Subscription>> GetWithVendorAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(s => s.UserId == userId)
            .Include(s => s.Vendor)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Subscription>> GetRequiringReviewAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(s => s.UserId == userId && s.RequiresUserReview)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetTotalMonthlySpendingAsync(string userId, CancellationToken cancellationToken = default)
    {
        var subscriptions = await _dbSet
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
            .ToListAsync(cancellationToken);

        return subscriptions.Sum(s => NormalizeToMonthly(s.Price, s.BillingCycle));
    }

    public async Task<Dictionary<string, decimal>> GetSpendingByCategoryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var subscriptions = await _dbSet
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
            .ToListAsync(cancellationToken);

        return subscriptions
            .GroupBy(s => string.IsNullOrEmpty(s.Category) ? "Uncategorized" : s.Category)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => NormalizeToMonthly(s.Price, s.BillingCycle))
            );
    }

    public async Task<Subscription?> FindDuplicateAsync(string serviceName, string emailAccountId, CancellationToken cancellationToken = default)
    {
        // Simple exact match for now - fuzzy matching can be added later
        return await _dbSet
            .FirstOrDefaultAsync(s => 
                s.ServiceName.ToLower() == serviceName.ToLower() 
                && s.EmailAccountId == emailAccountId
                && s.Status != SubscriptionStatus.Archived,
                cancellationToken);
    }

    public async Task ArchiveByEmailAccountAsync(string emailAccountId, CancellationToken cancellationToken = default)
    {
        var subscriptions = await _dbSet
            .Where(s => s.EmailAccountId == emailAccountId && s.Status != SubscriptionStatus.Archived)
            .ToListAsync(cancellationToken);

        foreach (var subscription in subscriptions)
        {
            subscription.Status = SubscriptionStatus.Archived;
            subscription.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Normalizes billing cycle to monthly equivalent
    /// Annual: Divide by 12, Quarterly: Divide by 3, Weekly: Multiply by 4.33
    /// </summary>
    private static decimal NormalizeToMonthly(decimal price, BillingCycle cycle)
    {
        return cycle switch
        {
            BillingCycle.Annual => price / 12m,
            BillingCycle.Quarterly => price / 3m,
            BillingCycle.Weekly => price * 4.33m,
            BillingCycle.Monthly => price,
            _ => price
        };
    }
}

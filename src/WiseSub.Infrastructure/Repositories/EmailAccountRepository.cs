using Microsoft.EntityFrameworkCore;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Entities;
using WiseSub.Infrastructure.Data;

namespace WiseSub.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for EmailAccount entity with token management
/// </summary>
public class EmailAccountRepository : Repository<EmailAccount>, IEmailAccountRepository
{
    public EmailAccountRepository(WiseSubDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<EmailAccount>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(ea => ea.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmailAccount?> GetByEmailAddressAsync(string emailAddress, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(ea => ea.EmailAddress == emailAddress, cancellationToken);
    }

    public async Task<IEnumerable<EmailAccount>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(ea => ea.UserId == userId && ea.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateTokensAsync(string accountId, string encryptedAccessToken, string encryptedRefreshToken, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        // Single database call using ExecuteUpdateAsync
        await _dbSet
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.EncryptedAccessToken, encryptedAccessToken)
                .SetProperty(a => a.EncryptedRefreshToken, encryptedRefreshToken)
                .SetProperty(a => a.TokenExpiresAt, expiresAt),
                cancellationToken);
    }

    public async Task UpdateLastScanAsync(string accountId, DateTime lastScanAt, CancellationToken cancellationToken = default)
    {
        // Single database call using ExecuteUpdateAsync
        await _dbSet
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.LastScanAt, lastScanAt),
                cancellationToken);
    }

    public async Task RevokeAccessAsync(string accountId, CancellationToken cancellationToken = default)
    {
        // Single database call using ExecuteUpdateAsync - deletes tokens immediately
        await _dbSet
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.EncryptedAccessToken, string.Empty)
                .SetProperty(a => a.EncryptedRefreshToken, string.Empty)
                .SetProperty(a => a.IsActive, false),
                cancellationToken);
    }

    public async Task UpdateHistoryIdAsync(string accountId, string historyId, CancellationToken cancellationToken = default)
    {
        // Single database call using ExecuteUpdateAsync
        await _dbSet
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.GmailHistoryId, historyId),
                cancellationToken);
    }
}

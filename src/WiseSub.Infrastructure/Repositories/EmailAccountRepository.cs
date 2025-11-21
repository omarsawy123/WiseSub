using Microsoft.EntityFrameworkCore;
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
        var account = await GetByIdAsync(accountId, cancellationToken);
        if (account != null)
        {
            account.EncryptedAccessToken = encryptedAccessToken;
            account.EncryptedRefreshToken = encryptedRefreshToken;
            account.TokenExpiresAt = expiresAt;
            await UpdateAsync(account, cancellationToken);
        }
    }

    public async Task UpdateLastScanAsync(string accountId, DateTime lastScanAt, CancellationToken cancellationToken = default)
    {
        var account = await GetByIdAsync(accountId, cancellationToken);
        if (account != null)
        {
            account.LastScanAt = lastScanAt;
            await UpdateAsync(account, cancellationToken);
        }
    }

    public async Task RevokeAccessAsync(string accountId, CancellationToken cancellationToken = default)
    {
        var account = await GetByIdAsync(accountId, cancellationToken);
        if (account != null)
        {
            // Delete tokens immediately
            account.EncryptedAccessToken = string.Empty;
            account.EncryptedRefreshToken = string.Empty;
            account.IsActive = false;
            await UpdateAsync(account, cancellationToken);
        }
    }
}

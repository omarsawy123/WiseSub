using Microsoft.EntityFrameworkCore;
using WiseSub.Domain.Entities;
using WiseSub.Infrastructure.Data;

namespace WiseSub.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for User entity
/// </summary>
public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(WiseSubDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetByOAuthAsync(string provider, string subjectId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(u => u.OAuthProvider == provider && u.OAuthSubjectId == subjectId, cancellationToken);
    }

    public async Task<User?> GetWithEmailAccountsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.EmailAccounts)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task<User?> GetWithSubscriptionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.Subscriptions)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task DeleteUserDataAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbSet
            .Include(u => u.EmailAccounts)
            .Include(u => u.Subscriptions)
            .Include(u => u.Alerts)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user != null)
        {
            _dbSet.Remove(user);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

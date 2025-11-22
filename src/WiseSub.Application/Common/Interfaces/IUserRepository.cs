using WiseSub.Domain.Entities;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Repository interface for User entity operations
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// Gets a user by email address
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a user by OAuth provider and subject ID
    /// </summary>
    Task<User?> GetByOAuthAsync(string provider, string subjectId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a user with all related email accounts
    /// </summary>
    Task<User?> GetWithEmailAccountsAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a user with all related subscriptions
    /// </summary>
    Task<User?> GetWithSubscriptionsAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a user and all related data (cascading delete)
    /// </summary>
    Task DeleteUserDataAsync(string userId, CancellationToken cancellationToken = default);
}

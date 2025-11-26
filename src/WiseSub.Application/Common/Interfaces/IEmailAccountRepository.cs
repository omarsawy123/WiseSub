using WiseSub.Domain.Entities;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Repository interface for EmailAccount entity operations
/// </summary>
public interface IEmailAccountRepository : IRepository<EmailAccount>
{
    /// <summary>
    /// Gets all email accounts for a specific user
    /// </summary>
    Task<IEnumerable<EmailAccount>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets an email account by email address
    /// </summary>
    Task<EmailAccount?> GetByEmailAddressAsync(string emailAddress, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all active email accounts for a user
    /// </summary>
    Task<IEnumerable<EmailAccount>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all active email accounts across all users (for background job scanning)
    /// </summary>
    Task<IEnumerable<EmailAccount>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates the access token for an email account
    /// </summary>
    Task UpdateTokensAsync(string accountId, string encryptedAccessToken, string encryptedRefreshToken, DateTime expiresAt, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates the last scan timestamp
    /// </summary>
    Task UpdateLastScanAsync(string accountId, DateTime lastScanAt, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Revokes access by deleting tokens and marking as inactive
    /// </summary>
    Task RevokeAccessAsync(string accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates the Gmail History ID for incremental sync
    /// </summary>
    Task UpdateHistoryIdAsync(string accountId, string historyId, CancellationToken cancellationToken = default);
}

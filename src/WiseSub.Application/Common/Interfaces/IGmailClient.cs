using WiseSub.Application.Common.Models;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Interface for Gmail API integration
/// </summary>
public interface IGmailClient
{
    /// <summary>
    /// Connects a Gmail account using OAuth authorization code
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="authorizationCode">OAuth authorization code from Google</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connection result with email account details</returns>
    Task<EmailConnectionResult> ConnectAccountAsync(
        string userId, 
        string authorizationCode, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves emails from a Gmail account
    /// </summary>
    /// <param name="emailAccountId">The email account ID</param>
    /// <param name="filter">Filter criteria for emails</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of email messages</returns>
    Task<IEnumerable<EmailMessage>> GetEmailsAsync(
        string emailAccountId, 
        EmailFilter filter, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves emails from a Gmail account (optimized - avoids redundant DB fetch)
    /// </summary>
    /// <param name="emailAccount">The email account entity</param>
    /// <param name="filter">Filter criteria for emails</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of email messages</returns>
    Task<IEnumerable<EmailMessage>> GetEmailsAsync(
        Domain.Entities.EmailAccount emailAccount,
        EmailFilter filter,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Refreshes the OAuth access token for a Gmail account
    /// </summary>
    /// <param name="emailAccountId">The email account ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if refresh was successful</returns>
    Task<bool> RefreshAccessTokenAsync(
        string emailAccountId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Revokes access to a Gmail account
    /// </summary>
    /// <param name="emailAccountId">The email account ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if revocation was successful</returns>
    Task<bool> RevokeAccessAsync(
        string emailAccountId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves new emails since last scan using Gmail History API (incremental sync)
    /// </summary>
    /// <param name="emailAccountId">The email account ID</param>
    /// <param name="filter">Filter criteria for emails</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of new email messages since last scan</returns>
    Task<IEnumerable<EmailMessage>> GetNewEmailsSinceLastScanAsync(
        string emailAccountId,
        EmailFilter filter,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves new emails since last scan using Gmail History API (optimized - avoids redundant DB fetch)
    /// </summary>
    /// <param name="emailAccount">The email account entity</param>
    /// <param name="filter">Filter criteria for emails</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of new email messages since last scan</returns>
    Task<IEnumerable<EmailMessage>> GetNewEmailsSinceLastScanAsync(
        Domain.Entities.EmailAccount emailAccount,
        EmailFilter filter,
        CancellationToken cancellationToken = default);
}

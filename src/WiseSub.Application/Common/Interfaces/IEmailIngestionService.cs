using WiseSub.Application.Common.Models;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Service for ingesting emails from connected email accounts
/// </summary>
public interface IEmailIngestionService
{
    /// <summary>
    /// Scans an email account for subscription-related emails
    /// </summary>
    /// <param name="emailAccountId">The email account ID to scan</param>
    /// <param name="since">Retrieve emails since this date (defaults to 12 months ago)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of emails retrieved</returns>
    Task<int> ScanEmailAccountAsync(
        string emailAccountId,
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans all active email accounts for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total number of emails retrieved across all accounts</returns>
    Task<int> ScanUserEmailAccountsAsync(
        string userId,
        CancellationToken cancellationToken = default);
}

using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Service for ingesting emails from connected email accounts
/// </summary>
public interface IEmailIngestionService
{
    /// <summary>
    /// Scans an email account for subscription-related emails
    /// </summary>
    /// <param name="emailAccount"></param>
    /// <param name="since"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<int>> ScanEmailAccountAsync(
        EmailAccount emailAccount,
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans all active email accounts for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total number of emails retrieved across all accounts</returns>
    Task<Result<int>> ScanUserEmailAccountsAsync(
        string userId,
        CancellationToken cancellationToken = default);
}

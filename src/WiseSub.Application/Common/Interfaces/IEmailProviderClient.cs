using WiseSub.Application.Common.Models;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Generic interface for email provider clients (Gmail, Outlook, etc.)
/// </summary>
public interface IEmailProviderClient
{
    /// <summary>
    /// Gets emails from the provider with the specified filter
    /// </summary>
    /// <param name="emailAccount">The email account to retrieve emails from</param>
    /// <param name="filter">Filter criteria for emails</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of email messages</returns>
    Task<IEnumerable<EmailMessage>> GetEmailsAsync(
        EmailAccount emailAccount,
        EmailFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets new emails since the last scan using incremental sync
    /// </summary>
    /// <param name="emailAccount">The email account to retrieve emails from</param>
    /// <param name="filter">Filter criteria for emails</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of new email messages</returns>
    Task<IEnumerable<EmailMessage>> GetNewEmailsSinceLastScanAsync(
        EmailAccount emailAccount,
        EmailFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this client supports the specified provider
    /// </summary>
    /// <param name="provider">The email provider to check</param>
    /// <returns>True if supported, false otherwise</returns>
    bool SupportsProvider(EmailProvider provider);
}

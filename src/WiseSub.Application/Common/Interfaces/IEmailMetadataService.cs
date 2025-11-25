using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Service for creating and managing email metadata with duplicate detection
/// </summary>
public interface IEmailMetadataService
{
    /// <summary>
    /// Creates email metadata for a single email, checking for duplicates
    /// </summary>
    /// <param name="emailAccountId">The email account ID</param>
    /// <param name="email">The email message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the created or existing email metadata</returns>
    Task<Result<EmailMetadata>> CreateEmailMetadataAsync(
        string emailAccountId,
        EmailMessage email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates email metadata for multiple emails in batch, filtering duplicates
    /// </summary>
    /// <param name="emailAccountId">The email account ID</param>
    /// <param name="emails">List of email messages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing list of newly created email metadata (excludes duplicates)</returns>
    Task<Result<List<EmailMetadata>>> CreateEmailMetadataBatchAsync(
        string emailAccountId,
        List<EmailMessage> emails,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an email metadata as processed
    /// </summary>
    /// <param name="emailMetadataId">The email metadata ID</param>
    /// <param name="subscriptionId">The subscription ID if one was created</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result> MarkAsProcessedAsync(
        string emailMetadataId,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the processing status of an email
    /// </summary>
    /// <param name="emailMetadataId">The email metadata ID</param>
    /// <param name="newStatus">The new status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result> UpdateStatusAsync(
        string emailMetadataId,
        EmailProcessingStatus newStatus,
        CancellationToken cancellationToken = default);
}

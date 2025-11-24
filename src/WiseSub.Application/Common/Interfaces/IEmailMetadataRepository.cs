using WiseSub.Domain.Entities;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Repository for email metadata operations
/// </summary>
public interface IEmailMetadataRepository : IRepository<EmailMetadata>
{
    /// <summary>
    /// Gets all unprocessed email metadata
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of unprocessed email metadata</returns>
    Task<IEnumerable<EmailMetadata>> GetUnprocessedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets email metadata by external email ID
    /// </summary>
    /// <param name="externalEmailId">The external email ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Email metadata if found, null otherwise</returns>
    Task<EmailMetadata?> GetByExternalEmailIdAsync(
        string externalEmailId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of processed emails
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of processed emails</returns>
    Task<int> GetProcessedCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of unprocessed emails
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of unprocessed emails</returns>
    Task<int> GetUnprocessedCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk adds multiple email metadata records in a single transaction
    /// </summary>
    /// <param name="emailMetadataList">List of email metadata to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    Task BulkAddAsync(
        List<EmailMetadata> emailMetadataList,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets existing external email IDs from a list of IDs (batch duplicate check)
    /// </summary>
    /// <param name="externalIds">List of external email IDs to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HashSet of existing external email IDs</returns>
    Task<HashSet<string>> GetExistingExternalProcessedIdsAsync(
        List<string> externalIds,
        CancellationToken cancellationToken = default);
}

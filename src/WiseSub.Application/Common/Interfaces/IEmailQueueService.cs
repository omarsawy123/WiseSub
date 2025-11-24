using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Service for queueing emails for AI processing with priority support
/// </summary>
public interface IEmailQueueService
{
    /// <summary>
    /// Queues an email for AI processing
    /// </summary>
    /// <param name="emailMetadata">The email metadata</param>
    /// <param name="email">The email message to queue</param>
    /// <param name="priority">Processing priority</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result</returns>
    Task<Result> QueueEmailForProcessingAsync(
        EmailMetadata emailMetadata,
        EmailMessage email,
        EmailProcessingPriority priority = EmailProcessingPriority.Normal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current queue status
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Queue status information</returns>
    Task<QueueStatus> GetQueueStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues the next email for processing based on priority
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The next email to process, or null if queue is empty</returns>
    Task<QueuedEmail?> DequeueNextEmailAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of pending emails in the queue
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of pending emails</returns>
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues multiple emails for processing in a single batch (optimized for performance)
    /// </summary>
    /// <param name="emailMetadataList">List of email metadata</param>
    /// <param name="emailsDict">Dictionary mapping external email ID to email message</param>
    /// <param name="priority">Processing priority</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of emails successfully queued</returns>
    Task<Result<int>> QueueEmailBatchAsync(
        List<EmailMetadata> emailMetadataList,
        Dictionary<string, EmailMessage> emailsDict,
        EmailProcessingPriority priority = EmailProcessingPriority.Normal,
        CancellationToken cancellationToken = default);
}

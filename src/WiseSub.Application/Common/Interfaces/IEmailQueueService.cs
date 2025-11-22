using WiseSub.Application.Common.Models;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Service for queueing emails for AI processing with priority support
/// </summary>
public interface IEmailQueueService
{
    /// <summary>
    /// Queues an email for AI processing
    /// </summary>
    /// <param name="emailAccountId">The email account ID</param>
    /// <param name="email">The email message to queue</param>
    /// <param name="priority">Processing priority</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The queued email metadata ID</returns>
    Task<string> QueueEmailForProcessingAsync(
        string emailAccountId,
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
    /// Marks an email as processed
    /// </summary>
    /// <param name="emailMetadataId">The email metadata ID</param>
    /// <param name="subscriptionId">The subscription ID if one was created</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsProcessedAsync(
        string emailMetadataId,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of pending emails in the queue
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of pending emails</returns>
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
}

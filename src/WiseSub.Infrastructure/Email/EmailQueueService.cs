using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;

namespace WiseSub.Infrastructure.Email;

/// <summary>
/// In-memory implementation of email queue service for MVP
/// Uses priority-based queuing with thread-safe collections
/// </summary>
public class EmailQueueService : IEmailQueueService
{
    private readonly ILogger<EmailQueueService> _logger;

    // Priority queues - higher priority processed first
    private readonly Channel<QueuedEmail> _highPriorityQueue = Channel.CreateBounded<QueuedEmail>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly Channel<QueuedEmail> _normalPriorityQueue = Channel.CreateBounded<QueuedEmail>(
        new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly Channel<QueuedEmail> _lowPriorityQueue = Channel.CreateBounded<QueuedEmail>(
        new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });



    public EmailQueueService(ILogger<EmailQueueService> logger)
    {
        _logger = logger;
    }

    public async Task<Result> QueueEmailForProcessingAsync(
        EmailMetadata emailMetadata,
        EmailMessage email,
        EmailProcessingPriority priority = EmailProcessingPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        if (emailMetadata == null)
            return Result.Failure(EmailMetadataErrors.NotFound);

        if (email == null)
            return Result.Failure(EmailMetadataErrors.InvalidFormat);

        // Create queued email
        var queuedEmail = new QueuedEmail
        {
            EmailMetadataId = emailMetadata.Id,
            EmailAccountId = emailMetadata.EmailAccountId,
            Email = email,
            Priority = priority,
            QueuedAt = DateTime.UtcNow
        };

        // Add to appropriate priority queue
        var queue = GetQueueForPriority(priority);
        await queue.Writer.WriteAsync(queuedEmail, cancellationToken);

        return Result.Success();
    }

    public Task<QueueStatus> GetQueueStatusAsync(CancellationToken cancellationToken = default)
    {
        var highCount = _highPriorityQueue.Reader.Count;
        var normalCount = _normalPriorityQueue.Reader.Count;
        var lowCount = _lowPriorityQueue.Reader.Count;
        
        var status = new QueueStatus
        {
            HighPriorityCount = highCount,
            NormalPriorityCount = normalCount,
            LowPriorityCount = lowCount,
            PendingCount = highCount + normalCount + lowCount,
            ProcessedCount = 0, // Processed count managed by metadata service
            Timestamp = DateTime.UtcNow
        };

        return Task.FromResult(status);
    }

    public Task<QueuedEmail?> DequeueNextEmailAsync(CancellationToken cancellationToken = default)
    {
        // Process in priority order: High -> Normal -> Low
        if (_highPriorityQueue.Reader.TryRead(out var highPriorityEmail))
        {
            _logger.LogDebug("Dequeued high priority email {EmailId}", highPriorityEmail.EmailMetadataId);
            return Task.FromResult<QueuedEmail?>(highPriorityEmail);
        }

        if (_normalPriorityQueue.Reader.TryRead(out var normalPriorityEmail))
        {
            _logger.LogDebug("Dequeued normal priority email {EmailId}", normalPriorityEmail.EmailMetadataId);
            return Task.FromResult<QueuedEmail?>(normalPriorityEmail);
        }

        if (_lowPriorityQueue.Reader.TryRead(out var lowPriorityEmail))
        {
            _logger.LogDebug("Dequeued low priority email {EmailId}", lowPriorityEmail.EmailMetadataId);
            return Task.FromResult<QueuedEmail?>(lowPriorityEmail);
        }

        return Task.FromResult<QueuedEmail?>(null);
    }

    public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        var total = _highPriorityQueue.Reader.Count + 
                    _normalPriorityQueue.Reader.Count + 
                    _lowPriorityQueue.Reader.Count;
        return Task.FromResult(total);
    }

    /// <summary>
    /// Efficiently waits for an email to become available in any queue.
    /// Uses WaitToReadAsync instead of polling to reduce CPU usage.
    /// </summary>
    public async Task<bool> WaitForEmailAsync(CancellationToken cancellationToken = default)
    {
        // First check if any queue has items (no-wait path for performance)
        if (_highPriorityQueue.Reader.Count > 0 ||
            _normalPriorityQueue.Reader.Count > 0 ||
            _lowPriorityQueue.Reader.Count > 0)
        {
            return true;
        }

        // Wait for any of the queues to have data available
        // This is much more efficient than polling with Task.Delay
        try
        {
            var highTask = _highPriorityQueue.Reader.WaitToReadAsync(cancellationToken).AsTask();
            var normalTask = _normalPriorityQueue.Reader.WaitToReadAsync(cancellationToken).AsTask();
            var lowTask = _lowPriorityQueue.Reader.WaitToReadAsync(cancellationToken).AsTask();

            var completedTask = await Task.WhenAny(highTask, normalTask, lowTask);
            return await completedTask;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public async Task<Result<int>> QueueEmailBatchAsync(
        List<EmailMetadata> emailMetadataList,
        Dictionary<string, EmailMessage> emailsDict,
        EmailProcessingPriority priority = EmailProcessingPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        if (emailMetadataList == null || !emailMetadataList.Any())
            return Result.Success(0);

        if (emailsDict == null || !emailsDict.Any())
            return Result.Success(0);

        // Add to in-memory priority queue
        var queue = GetQueueForPriority(priority);
        
        foreach (var metadata in emailMetadataList)
        {
            // Get the corresponding email message
            if (!emailsDict.TryGetValue(metadata.ExternalEmailId, out var email))
            {
                _logger.LogWarning(
                    "Email message not found for metadata {MetadataId} with external ID {ExternalId}",
                    metadata.Id, metadata.ExternalEmailId);
                continue;
            }

            var queuedEmail = new QueuedEmail
            {
                EmailMetadataId = metadata.Id,
                EmailAccountId = metadata.EmailAccountId,
                Email = email,
                Priority = priority,
                QueuedAt = DateTime.UtcNow
            };

            await queue.Writer.WriteAsync(queuedEmail, cancellationToken);
        }

        _logger.LogInformation(
            "Successfully queued {QueuedCount} emails with priority {Priority}",
            emailMetadataList.Count, priority);

        return Result.Success(emailMetadataList.Count);
    }

    private Channel<QueuedEmail> GetQueueForPriority(EmailProcessingPriority priority)
    {
        return priority switch
        {
            EmailProcessingPriority.High => _highPriorityQueue,
            EmailProcessingPriority.Normal => _normalPriorityQueue,
            EmailProcessingPriority.Low => _lowPriorityQueue,
            _ => _normalPriorityQueue
        };
    }

}

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Entities;

namespace WiseSub.Infrastructure.Email;

/// <summary>
/// In-memory implementation of email queue service for MVP
/// Uses priority-based queuing with thread-safe collections
/// </summary>
public class EmailQueueService : IEmailQueueService
{
    private readonly ILogger<EmailQueueService> _logger;
    private readonly IEmailMetadataRepository _emailMetadataRepository;

    // Priority queues - higher priority processed first
    private readonly ConcurrentQueue<QueuedEmail> _highPriorityQueue = new();
    private readonly ConcurrentQueue<QueuedEmail> _normalPriorityQueue = new();
    private readonly ConcurrentQueue<QueuedEmail> _lowPriorityQueue = new();

    // Track queued email IDs to prevent duplicates
    private readonly ConcurrentDictionary<string, bool> _queuedEmailIds = new();

    public EmailQueueService(
        ILogger<EmailQueueService> logger,
        IEmailMetadataRepository emailMetadataRepository)
    {
        _logger = logger;
        _emailMetadataRepository = emailMetadataRepository;
    }

    public async Task<string> QueueEmailForProcessingAsync(
        string emailAccountId,
        EmailMessage email,
        EmailProcessingPriority priority = EmailProcessingPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        // Check if email already exists in metadata (avoid duplicates)
        var existingMetadata = await _emailMetadataRepository.GetByExternalEmailIdAsync(
            email.Id, cancellationToken);

        if (existingMetadata != null)
        {
            _logger.LogDebug(
                "Email {EmailId} already exists in metadata, skipping queue",
                email.Id);
            return existingMetadata.Id;
        }

        // Create email metadata record
        var emailMetadata = new EmailMetadata
        {
            Id = Guid.NewGuid().ToString(),
            EmailAccountId = emailAccountId,
            ExternalEmailId = email.Id,
            Sender = email.Sender,
            Subject = email.Subject,
            ReceivedAt = email.ReceivedAt,
            IsProcessed = false,
            ProcessedAt = null,
            SubscriptionId = null
        };

        // Save to database
        await _emailMetadataRepository.AddAsync(emailMetadata, cancellationToken);

        // Create queued email
        var queuedEmail = new QueuedEmail
        {
            EmailMetadataId = emailMetadata.Id,
            EmailAccountId = emailAccountId,
            Email = email,
            Priority = priority,
            QueuedAt = DateTime.UtcNow
        };

        // Add to appropriate priority queue
        var queue = GetQueueForPriority(priority);
        queue.Enqueue(queuedEmail);
        _queuedEmailIds.TryAdd(emailMetadata.Id, true);

        _logger.LogInformation(
            "Queued email {EmailId} with priority {Priority} for processing. Metadata ID: {MetadataId}",
            email.Id, priority, emailMetadata.Id);

        return emailMetadata.Id;
    }

    public async Task<QueueStatus> GetQueueStatusAsync(CancellationToken cancellationToken = default)
    {
        var processedCount = await _emailMetadataRepository.GetProcessedCountAsync(cancellationToken);

        var status = new QueueStatus
        {
            HighPriorityCount = _highPriorityQueue.Count,
            NormalPriorityCount = _normalPriorityQueue.Count,
            LowPriorityCount = _lowPriorityQueue.Count,
            PendingCount = _highPriorityQueue.Count + _normalPriorityQueue.Count + _lowPriorityQueue.Count,
            ProcessedCount = processedCount,
            Timestamp = DateTime.UtcNow
        };

        return status;
    }

    public Task<QueuedEmail?> DequeueNextEmailAsync(CancellationToken cancellationToken = default)
    {
        // Process in priority order: High -> Normal -> Low
        QueuedEmail? queuedEmail = null;

        if (_highPriorityQueue.TryDequeue(out var highPriorityEmail))
        {
            queuedEmail = highPriorityEmail;
        }
        else if (_normalPriorityQueue.TryDequeue(out var normalPriorityEmail))
        {
            queuedEmail = normalPriorityEmail;
        }
        else if (_lowPriorityQueue.TryDequeue(out var lowPriorityEmail))
        {
            queuedEmail = lowPriorityEmail;
        }

        if (queuedEmail != null)
        {
            _queuedEmailIds.TryRemove(queuedEmail.EmailMetadataId, out _);
            
            _logger.LogDebug(
                "Dequeued email {EmailMetadataId} with priority {Priority}",
                queuedEmail.EmailMetadataId, queuedEmail.Priority);
        }

        return Task.FromResult(queuedEmail);
    }

    public async Task MarkAsProcessedAsync(
        string emailMetadataId,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var emailMetadata = await _emailMetadataRepository.GetByIdAsync(
            emailMetadataId, cancellationToken);

        if (emailMetadata == null)
        {
            _logger.LogWarning(
                "Email metadata {EmailMetadataId} not found when marking as processed",
                emailMetadataId);
            return;
        }

        emailMetadata.IsProcessed = true;
        emailMetadata.ProcessedAt = DateTime.UtcNow;
        emailMetadata.SubscriptionId = subscriptionId;

        await _emailMetadataRepository.UpdateAsync(emailMetadata, cancellationToken);

        _logger.LogInformation(
            "Marked email {EmailMetadataId} as processed. Subscription ID: {SubscriptionId}",
            emailMetadataId, subscriptionId ?? "None");
    }

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetQueueStatusAsync(cancellationToken);
        return status.PendingCount;
    }

    private ConcurrentQueue<QueuedEmail> GetQueueForPriority(EmailProcessingPriority priority)
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

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
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
    private readonly IEmailMetadataRepository _emailMetadataRepository;

    // Priority queues - higher priority processed first
    private readonly Channel<QueuedEmail> _highPriorityQueue = Channel.CreateBounded<QueuedEmail>(
        new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    private readonly Channel<QueuedEmail> _normalPriorityQueue = Channel.CreateBounded<QueuedEmail>(
        new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    private readonly Channel<QueuedEmail> _lowPriorityQueue = Channel.CreateBounded<QueuedEmail>(
        new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    // Track queued email IDs to prevent duplicates
    private readonly ConcurrentDictionary<string, bool> _queuedEmailIds = new();

    public EmailQueueService(
        ILogger<EmailQueueService> logger,
        IEmailMetadataRepository emailMetadataRepository)
    {
        _logger = logger;
        _emailMetadataRepository = emailMetadataRepository;
    }

    public async Task<Result<string>> QueueEmailForProcessingAsync(
        string emailAccountId,
        EmailMessage email,
        EmailProcessingPriority priority = EmailProcessingPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        if (email == null)
            return Result.Failure<string>(EmailMetadataErrors.InvalidFormat);

        // Check if email already exists in metadata (avoid duplicates)
        var existingMetadata = await _emailMetadataRepository.GetByExternalEmailIdAsync(
            email.Id, cancellationToken);

        if (existingMetadata != null)
        {
            _logger.LogDebug(
                "Email {EmailId} already exists in metadata, skipping queue",
                email.Id);
            return Result.Success(existingMetadata.Id);
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
        await queue.Writer.WriteAsync(queuedEmail, cancellationToken);
        _queuedEmailIds.TryAdd(emailMetadata.Id, true);

        _logger.LogInformation(
            "Queued email {EmailId} with priority {Priority} for processing. Metadata ID: {MetadataId}",
            email.Id, priority, emailMetadata.Id);

        return Result.Success(emailMetadata.Id);
    }

    public async Task<QueueStatus> GetQueueStatusAsync(CancellationToken cancellationToken = default)
    {
        var processedCount = await _emailMetadataRepository.GetProcessedCountAsync(cancellationToken);

        var status = new QueueStatus
        {
            HighPriorityCount = _highPriorityQueue.Reader.Count,
            NormalPriorityCount = _normalPriorityQueue.Reader.Count,
            LowPriorityCount = _lowPriorityQueue.Reader.Count,
            PendingCount = _highPriorityQueue.Reader.Count + _normalPriorityQueue.Reader.Count + _lowPriorityQueue.Reader.Count,
            ProcessedCount = processedCount,
            Timestamp = DateTime.UtcNow
        };

        return status;
    }

    public Task<QueuedEmail?> DequeueNextEmailAsync(CancellationToken cancellationToken = default)
    {
        // Process in priority order: High -> Normal -> Low
        QueuedEmail? queuedEmail = null;

        return Task.FromResult(queuedEmail);
    }

    public async Task<Result> MarkAsProcessedAsync(
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
            return Result.Failure(EmailMetadataErrors.NotFound);
        }

        if (emailMetadata.IsProcessed)
        {
            _logger.LogWarning(
                "Email metadata {EmailMetadataId} is already processed",
                emailMetadataId);
            return Result.Failure(EmailMetadataErrors.AlreadyProcessed);
        }

        emailMetadata.IsProcessed = true;
        emailMetadata.ProcessedAt = DateTime.UtcNow;
        emailMetadata.SubscriptionId = subscriptionId;

        await _emailMetadataRepository.UpdateAsync(emailMetadata, cancellationToken);

        _logger.LogInformation(
            "Marked email {EmailMetadataId} as processed. Subscription ID: {SubscriptionId}",
            emailMetadataId, subscriptionId ?? "None");
        
        return Result.Success();
    }

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetQueueStatusAsync(cancellationToken);
        return status.PendingCount;
    }

    public async Task<Result<int>> QueueEmailBatchAsync(
        string emailAccountId,
        List<EmailMessage> emails,
        EmailProcessingPriority priority = EmailProcessingPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        if (emails == null || !emails.Any())
            return Result.Success(0);

        _logger.LogInformation("Starting batch queue of {Count} emails for account {EmailAccountId}",
            emails.Count, emailAccountId);

        // Batch duplicate check - get all external IDs that already exist
        var externalIds = emails.Select(e => e.Id).ToList();
        var existingIds = await _emailMetadataRepository
            .GetExistingExternalIdsAsync(externalIds, cancellationToken);

        _logger.LogDebug("Found {ExistingCount} existing emails out of {TotalCount}",
            existingIds.Count, emails.Count);

        // Filter to only new emails
        var newEmails = emails
            .Where(e => !existingIds.Contains(e.Id))
            .ToList();

        if (!newEmails.Any())
        {
            _logger.LogInformation("No new emails to queue for account {EmailAccountId}", emailAccountId);
            return Result.Success(0);
        }

        // Bulk create email metadata
        var metadataList = newEmails.Select(email => new EmailMetadata
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
        }).ToList();

        // Bulk insert to database
        await _emailMetadataRepository.BulkAddAsync(metadataList, cancellationToken);

        var newEmailsDict = newEmails.ToDictionary(e => e.Id, e => e);

        // Add to in-memory priority queue
        var queue = GetQueueForPriority(priority);
        foreach (var metadata in metadataList)
        {
            var queuedEmail = new QueuedEmail
            {
                EmailMetadataId = metadata.Id,
                EmailAccountId = emailAccountId,
                Email = newEmailsDict[metadata.ExternalEmailId],
                Priority = priority,
                QueuedAt = DateTime.UtcNow
            };

            await queue.Writer.WriteAsync(queuedEmail, cancellationToken);
            _queuedEmailIds.TryAdd(metadata.Id, true);
        }

        _logger.LogInformation(
            "Successfully queued {QueuedCount} new emails with priority {Priority} for account {EmailAccountId}",
            newEmails.Count, priority, emailAccountId);

        return Result.Success(newEmails.Count);
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

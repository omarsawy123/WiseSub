using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Services;

/// <summary>
/// Service for creating and managing email metadata with duplicate detection
/// </summary>
public class EmailMetadataService : IEmailMetadataService
{
    private readonly ILogger<EmailMetadataService> _logger;
    private readonly IEmailMetadataRepository _emailMetadataRepository;

    public EmailMetadataService(
        ILogger<EmailMetadataService> logger,
        IEmailMetadataRepository emailMetadataRepository)
    {
        _logger = logger;
        _emailMetadataRepository = emailMetadataRepository;
    }

    public async Task<Result<EmailMetadata>> CreateEmailMetadataAsync(
        string emailAccountId,
        EmailMessage email,
        CancellationToken cancellationToken = default)
    {
        if (email == null)
            return Result.Failure<EmailMetadata>(EmailMetadataErrors.InvalidFormat);

        // Check if email already exists in metadata (avoid duplicates)
        var existingMetadata = await _emailMetadataRepository.GetByExternalEmailIdAsync(
            email.Id, cancellationToken);

        if (existingMetadata != null)
        {
            return Result.Success(existingMetadata);
        }

        // Create email metadata record
        var emailMetadata = MapToEmailMetadata(emailAccountId, email);

        // Save to database
        await _emailMetadataRepository.AddAsync(emailMetadata, cancellationToken);

        return Result.Success(emailMetadata);
    }

    public async Task<Result<List<EmailMetadata>>> CreateEmailMetadataBatchAsync(
        string emailAccountId,
        List<EmailMessage> emails,
        CancellationToken cancellationToken = default)
    {
        if (emails == null || !emails.Any())
            return Result.Success(new List<EmailMetadata>());

        var externalIds = emails.Select(e => e.Id).ToList();
        
        // STEP 1: Single optimized query
        // Returns: Pending, Queued, Failed emails (NOT Completed, NOT Processing)
        var existingUnprocessed = await _emailMetadataRepository
            .GetUnprocessedByExternalIdsAsync(externalIds, cancellationToken);
        
        // STEP 2: In-memory separation
        var existingDict = existingUnprocessed.ToDictionary(m => m.ExternalEmailId);
        
        // Truly new emails = not in existingDict
        var trulyNew = emails
            .Where(e => !existingDict.ContainsKey(e.Id))
            .ToList();
        
        // STEP 3: Create metadata for new emails
        var newMetadata = trulyNew
            .Select(email => MapToEmailMetadata(emailAccountId, email))
            .ToList();
        
        if (newMetadata.Any())
        {
            await _emailMetadataRepository.BulkAddAsync(newMetadata, cancellationToken);
        }
        
        // STEP 4: Return BOTH new + existing unprocessed for queueing
        var allToQueue = newMetadata.Concat(existingUnprocessed).ToList();
        
        _logger.LogInformation(
            "Prepared {Total} emails for queue: {New} new, {Existing} unprocessed",
            allToQueue.Count, newMetadata.Count, existingUnprocessed.Count);
        
        return Result.Success(allToQueue);
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

        if (emailMetadata.Status == EmailProcessingStatus.Completed)
        {
            _logger.LogWarning(
                "Email metadata {EmailMetadataId} is already processed",
                emailMetadataId);
            return Result.Failure(EmailMetadataErrors.AlreadyProcessed);
        }

        emailMetadata.Status = EmailProcessingStatus.Completed;
        emailMetadata.ProcessedAt = DateTime.UtcNow;
        emailMetadata.SubscriptionId = subscriptionId;

        await _emailMetadataRepository.UpdateAsync(emailMetadata, cancellationToken);
        
        return Result.Success();
    }

    public async Task<Result> UpdateStatusAsync(
        string emailMetadataId,
        EmailProcessingStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var emailMetadata = await _emailMetadataRepository.GetByIdAsync(emailMetadataId, cancellationToken);
        
        if (emailMetadata == null)
        {
            return Result.Failure(EmailMetadataErrors.NotFound);
        }

        var oldStatus = emailMetadata.Status;
        emailMetadata.Status = newStatus;

        // Update ProcessedAt timestamp if transitioning to Completed
        if (newStatus == EmailProcessingStatus.Completed)
        {
            emailMetadata.ProcessedAt = DateTime.UtcNow;
        }

        await _emailMetadataRepository.UpdateAsync(emailMetadata, cancellationToken);

        _logger.LogInformation(
            "Updated email {EmailMetadataId} status from {OldStatus} to {NewStatus}",
            emailMetadataId, oldStatus, newStatus);

        return Result.Success();
    }

    /// <summary>
    /// Maps EmailMessage to EmailMetadata entity
    /// </summary>
    private static EmailMetadata MapToEmailMetadata(string emailAccountId, EmailMessage email)
    {
        return new EmailMetadata
        {
            Id = Guid.NewGuid().ToString(),
            EmailAccountId = emailAccountId,
            ExternalEmailId = email.Id,
            Sender = email.Sender,
            Subject = email.Subject,
            ReceivedAt = email.ReceivedAt,
            Status = EmailProcessingStatus.Pending,  // Initial status
            ProcessedAt = null,
            SubscriptionId = null
        };
    }
}

using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;

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

        // Batch duplicate check - get all external IDs that already exist
        var externalIds = emails.Select(e => e.Id).ToList();
        var existingIds = await _emailMetadataRepository
            .GetExistingExternalProcessedIdsAsync(externalIds, cancellationToken);

        // Filter to only new emails
        var newEmails = emails
            .Where(e => !existingIds.Contains(e.Id))
            .ToList();

        if (!newEmails.Any())
        {
            return Result.Success(new List<EmailMetadata>());
        }

        // Bulk create email metadata
        var metadataList = newEmails
            .Select(email => MapToEmailMetadata(emailAccountId, email))
            .ToList();

        // Bulk insert to database
        await _emailMetadataRepository.BulkAddAsync(metadataList, cancellationToken);

        _logger.LogInformation(
            "Created {NewCount} email metadata records out of {TotalCount} emails",
            newEmails.Count, emails.Count);

        return Result.Success(metadataList);
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
            IsProcessed = false,
            ProcessedAt = null,
            SubscriptionId = null
        };
    }
}

using Hangfire;
using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Enums;

namespace WiseSub.Infrastructure.BackgroundServices.Jobs;

/// <summary>
/// Background job that processes a single email through AI classification and extraction.
/// This replaces the in-memory queue processing with Hangfire-based job processing.
/// </summary>
public class EmailProcessingJob
{
    private readonly ILogger<EmailProcessingJob> _logger;
    private readonly IEmailMetadataRepository _emailMetadataRepository;
    private readonly IEmailMetadataService _emailMetadataService;
    private readonly IAIExtractionService _aiExtractionService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IEmailAccountRepository _emailAccountRepository;

    public EmailProcessingJob(
        ILogger<EmailProcessingJob> logger,
        IEmailMetadataRepository emailMetadataRepository,
        IEmailMetadataService emailMetadataService,
        IAIExtractionService aiExtractionService,
        ISubscriptionService subscriptionService,
        IEmailAccountRepository emailAccountRepository)
    {
        _logger = logger;
        _emailMetadataRepository = emailMetadataRepository;
        _emailMetadataService = emailMetadataService;
        _aiExtractionService = aiExtractionService;
        _subscriptionService = subscriptionService;
        _emailAccountRepository = emailAccountRepository;
    }

    /// <summary>
    /// Processes a single email through AI classification and subscription extraction.
    /// </summary>
    /// <param name="emailMetadataId">The ID of the email metadata to process</param>
    /// <param name="emailAccountId">The ID of the email account</param>
    /// <param name="priority">Processing priority (for logging/metrics)</param>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    [Queue("email-processing")]
    public async Task ProcessEmailAsync(
        string emailMetadataId,
        string emailAccountId,
        EmailProcessingPriority priority = EmailProcessingPriority.Normal)
    {
        _logger.LogInformation(
            "Starting to process email {EmailMetadataId} with priority {Priority}",
            emailMetadataId, priority);

        try
        {
            // Get email metadata
            var metadata = await _emailMetadataRepository.GetByIdAsync(emailMetadataId);
            if (metadata == null)
            {
                _logger.LogWarning("Email metadata {EmailMetadataId} not found", emailMetadataId);
                return;
            }

            // Skip if already processed
            if (metadata.Status == EmailProcessingStatus.Completed)
            {
                _logger.LogInformation("Email {EmailMetadataId} already processed, skipping", emailMetadataId);
                return;
            }

            // Update status to Processing
            await _emailMetadataService.UpdateStatusAsync(
                emailMetadataId,
                EmailProcessingStatus.Processing);

            // Get email account for user context
            var emailAccount = await _emailAccountRepository.GetByIdAsync(emailAccountId);
            if (emailAccount == null)
            {
                _logger.LogWarning("Email account {EmailAccountId} not found", emailAccountId);
                await _emailMetadataService.UpdateStatusAsync(emailMetadataId, EmailProcessingStatus.Failed);
                return;
            }

            // Create email message from metadata for AI processing
            var emailMessage = new EmailMessage
            {
                Id = metadata.ExternalEmailId,
                Subject = metadata.Subject,
                Sender = metadata.Sender,
                ReceivedAt = metadata.ReceivedAt,
                // Body is not stored in metadata (privacy), so we use subject for classification
                Body = metadata.Subject // AI will work with subject for initial classification
            };

            // Step 1: Classify email
            var classificationResult = await _aiExtractionService.ClassifyEmailAsync(emailMessage);

            if (!classificationResult.IsSuccess || classificationResult.Value == null)
            {
                _logger.LogWarning(
                    "Classification failed for email {EmailMetadataId}: {Error}",
                    emailMetadataId, classificationResult.ErrorMessage);
                await _emailMetadataService.UpdateStatusAsync(emailMetadataId, EmailProcessingStatus.Failed);
                return;
            }

            var classification = classificationResult.Value;

            _logger.LogInformation(
                "Email {EmailMetadataId} classified: IsSubscription={IsSubscription}, Confidence={Confidence:F2}",
                emailMetadataId, classification.IsSubscriptionRelated, classification.Confidence);

            // If not subscription-related, mark as completed
            if (!classification.IsSubscriptionRelated)
            {
                await _emailMetadataService.MarkAsProcessedAsync(emailMetadataId, subscriptionId: null);
                _logger.LogInformation("Email {EmailMetadataId} is not subscription-related, marked as processed", emailMetadataId);
                return;
            }

            // Step 2: Extract subscription data
            var extractionResult = await _aiExtractionService.ExtractSubscriptionDataAsync(emailMessage);

            if (!extractionResult.IsSuccess || extractionResult.Value == null)
            {
                _logger.LogWarning(
                    "Extraction failed for email {EmailMetadataId}: {Error}",
                    emailMetadataId, extractionResult.ErrorMessage);
                await _emailMetadataService.UpdateStatusAsync(emailMetadataId, EmailProcessingStatus.Failed);
                return;
            }

            var extraction = extractionResult.Value;

            _logger.LogInformation(
                "Extracted subscription from email {EmailMetadataId}: {ServiceName}, {Price} {Currency}",
                emailMetadataId, extraction.ServiceName, extraction.Price, extraction.Currency);

            // Step 3: Create or update subscription
            var subscriptionResult = await _subscriptionService.CreateOrUpdateFromExtractionAsync(
                emailAccount.UserId,
                extraction,
                emailMetadataId);

            if (subscriptionResult.IsSuccess)
            {
                await _emailMetadataService.MarkAsProcessedAsync(
                    emailMetadataId,
                    subscriptionResult.Value?.Id);

                _logger.LogInformation(
                    "Successfully processed email {EmailMetadataId}, subscription {SubscriptionId}",
                    emailMetadataId, subscriptionResult.Value?.Id);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to create subscription from email {EmailMetadataId}: {Error}",
                    emailMetadataId, subscriptionResult.ErrorMessage);
                await _emailMetadataService.UpdateStatusAsync(emailMetadataId, EmailProcessingStatus.Failed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing email {EmailMetadataId}", emailMetadataId);
            
            try
            {
                await _emailMetadataService.UpdateStatusAsync(emailMetadataId, EmailProcessingStatus.Failed);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update status for email {EmailMetadataId}", emailMetadataId);
            }
            
            throw; // Re-throw to trigger Hangfire retry
        }
    }

    /// <summary>
    /// Processes all pending emails for a specific email account.
    /// Schedules individual ProcessEmailAsync jobs for each pending email.
    /// </summary>
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 60, 300 })]
    public async Task ProcessPendingEmailsForAccountAsync(string emailAccountId)
    {
        _logger.LogInformation("Processing pending emails for account {EmailAccountId}", emailAccountId);

        try
        {
            var pendingEmails = await _emailMetadataRepository.GetUnprocessedAsync();
            var accountEmails = pendingEmails.Where(e => e.EmailAccountId == emailAccountId).ToList();

            _logger.LogInformation(
                "Found {Count} pending emails for account {EmailAccountId}",
                accountEmails.Count, emailAccountId);

            foreach (var email in accountEmails)
            {
                // Schedule individual email processing with appropriate priority
                var priority = DeterminePriority(email);
                
                BackgroundJob.Enqueue<EmailProcessingJob>(
                    job => job.ProcessEmailAsync(email.Id, emailAccountId, priority));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending emails for account {EmailAccountId}", emailAccountId);
            throw;
        }
    }

    private static EmailProcessingPriority DeterminePriority(Domain.Entities.EmailMetadata email)
    {
        // Recent emails get higher priority
        var age = DateTime.UtcNow - email.ReceivedAt;
        
        if (age < TimeSpan.FromHours(24))
            return EmailProcessingPriority.High;
        if (age < TimeSpan.FromDays(7))
            return EmailProcessingPriority.Normal;
        
        return EmailProcessingPriority.Low;
    }
}

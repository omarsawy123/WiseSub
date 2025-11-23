using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Infrastructure.Email;

/// <summary>
/// Service for ingesting emails from connected email accounts
/// </summary>
public class EmailIngestionService : IEmailIngestionService
{
    private readonly ILogger<EmailIngestionService> _logger;
    private readonly IEmailAccountRepository _emailAccountRepository;
    private readonly IGmailClient _gmailClient;
    private readonly IEmailQueueService _emailQueueService;

    // Default subscription-related keywords for filtering
    private static readonly List<string> DefaultSubjectKeywords = new()
    {
        "subscription", "renewal", "invoice", "receipt", "payment",
        "billing", "charge", "trial", "upgrade", "membership",
        "plan", "premium", "pro", "plus"
    };

    public EmailIngestionService(
        ILogger<EmailIngestionService> logger,
        IEmailAccountRepository emailAccountRepository,
        IGmailClient gmailClient,
        IEmailQueueService emailQueueService)
    {
        _logger = logger;
        _emailAccountRepository = emailAccountRepository;
        _gmailClient = gmailClient;
        _emailQueueService = emailQueueService;
    }

    public async Task<int> ScanEmailAccountAsync(
        EmailAccount emailAccount,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting email scan for account {EmailAccountId}", emailAccount.Id);

        if (!emailAccount.IsActive)
        {
            _logger.LogWarning("Email account {EmailAccountId} is not active", emailAccount.Id);
            return 0;
        }

        // Default to 12 months ago if not specified
        var scanSince = since ?? DateTime.UtcNow.AddMonths(-12);

        // Create email filter
        var filter = new EmailFilter
        {
            Since = scanSince,
            SubjectKeywords = DefaultSubjectKeywords,
            MaxResults = 500 // Limit to 500 emails per scan
        };

        // Retrieve emails based on provider
        IEnumerable<EmailMessage> emails;
        if (emailAccount.Provider == EmailProvider.Gmail)
        {
            // Use incremental sync if this is not the first scan
            if (emailAccount.LastScanAt > DateTime.MinValue && !string.IsNullOrEmpty(emailAccount.GmailHistoryId))
            {
                _logger.LogInformation("Using incremental sync for account {EmailAccountId}", emailAccount.Id);
                emails = await _gmailClient.GetNewEmailsSinceLastScanAsync(emailAccount, filter, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Performing full scan for account {EmailAccountId}", emailAccount.Id);
                emails = await _gmailClient.GetEmailsAsync(emailAccount, filter, cancellationToken);
            }
        }
        else
        {
            _logger.LogWarning("Email provider {Provider} not yet supported", emailAccount.Provider);
            return 0;
        }

        var emailList = emails.ToList();
        _logger.LogInformation("Retrieved {Count} emails for account {EmailAccountId}",
            emailList.Count, emailAccount.Id);

        // Queue emails for AI processing with high priority (new subscriptions)
        var queuedCount = 0;
        foreach (var email in emailList)
        {
            await _emailQueueService.QueueEmailForProcessingAsync(
                emailAccount.Id,
                email,
                EmailProcessingPriority.High, // High priority for initial scan
                cancellationToken);
            queuedCount++;
        }

        _logger.LogInformation("Queued {QueuedCount} out of {TotalCount} emails for processing",
            queuedCount, emailList.Count);

        return emailList.Count;
    }

    public async Task<int> ScanUserEmailAccountsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting email scan for all accounts of user {UserId}", userId);

        // Get all active email accounts for user
        var emailAccounts = await _emailAccountRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        var accountList = emailAccounts.ToList();

        if (!accountList.Any())
        {
            _logger.LogInformation("No active email accounts found for user {UserId}", userId);
            return 0;
        }

        _logger.LogInformation("Found {Count} active email accounts for user {UserId}",
            accountList.Count, userId);

        // Scan each account
        var totalEmails = 0;
        foreach (var account in accountList)
        {
            var emailCount = await ScanEmailAccountAsync(account, null, cancellationToken);
            totalEmails += emailCount;

            // Small delay between accounts to avoid rate limiting
            if (accountList.Count > 1)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }

        _logger.LogInformation("Completed email scan for user {UserId}. Total emails: {TotalEmails}",
            userId, totalEmails);

        return totalEmails;
    }
}

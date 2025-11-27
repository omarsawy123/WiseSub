using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WiseSub.Application.Common.Configuration;
using WiseSub.Application.Common.Extensions;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;
using WiseSub.Infrastructure.BackgroundServices.Jobs;

namespace WiseSub.Infrastructure.Email;

/// <summary>
/// Service for ingesting emails from connected email accounts.
/// Uses Hangfire for background job processing instead of in-memory queues.
/// </summary>
public class EmailIngestionService : IEmailIngestionService
{
    private readonly ILogger<EmailIngestionService> _logger;
    private readonly IEmailAccountRepository _emailAccountRepository;
    private readonly IEmailProviderFactory _providerFactory;
    private readonly IEmailMetadataService _emailMetadataService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly EmailScanConfiguration _config;

    public EmailIngestionService(
        ILogger<EmailIngestionService> logger,
        IEmailAccountRepository emailAccountRepository,
        IEmailProviderFactory providerFactory,
        IEmailMetadataService emailMetadataService,
        IBackgroundJobClient backgroundJobClient,
        IOptions<EmailScanConfiguration> config)
    {
        _logger = logger;
        _emailAccountRepository = emailAccountRepository;
        _providerFactory = providerFactory;
        _emailMetadataService = emailMetadataService;
        _backgroundJobClient = backgroundJobClient;
        _config = config.Value;
    }

    public async Task<Result<int>> ScanEmailAccountAsync(
        EmailAccount emailAccount,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting email scan for account {EmailAccountId}", emailAccount.Id);

        if (emailAccount == null)
            return Result.Failure<int>(EmailAccountErrors.NotFound);

        if (!emailAccount.IsActive)
        {
            _logger.LogWarning("Email account {EmailAccountId} is not active", emailAccount.Id);
            return Result.Failure<int>(EmailAccountErrors.ConnectionFailed);
        }

        // Default to configured lookback if not specified
        var scanSince = since ?? DateTime.UtcNow.AddMonths(-_config.DefaultLookbackMonths);

        // Create email filter
        var filter = new EmailFilter
        {
            Since = scanSince,
            SubjectKeywords = _config.SubjectKeywords,
            MaxResults = _config.MaxEmailsPerScan
        };

        // Get provider client for the account
        IEmailProviderClient provider;
        try
        {
            provider = _providerFactory.GetProvider(emailAccount.Provider);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning("Email provider {Provider} not supported: {Error}",
                emailAccount.Provider, ex.Message);
            return Result.Failure<int>(EmailAccountErrors.InvalidProvider);
        }

        // Use incremental sync if supported
        IEnumerable<EmailMessage> emails;
        if (emailAccount.SupportsIncrementalSync())
        {
            _logger.LogInformation("Using incremental sync for account {EmailAccountId}", emailAccount.Id);
            emails = await provider.GetNewEmailsSinceLastScanAsync(emailAccount, filter, cancellationToken);
        }
        else
        {
            emails = await provider.GetEmailsAsync(emailAccount, filter, cancellationToken);
        }

        var emailList = emails.ToList();

        if (!emailList.Any())
        {
            return Result.Success(0);
        }

        // Step 1: Create email metadata with duplicate detection
        var metadataResult = await _emailMetadataService.CreateEmailMetadataBatchAsync(
            emailAccount.Id,
            emailList,
            cancellationToken);

        if (!metadataResult.IsSuccess || !metadataResult.Value.Any())
        {
            return Result.Success(emailList.Count);
        }

        var metadataList = metadataResult.Value;

        // Step 2: Schedule Hangfire jobs for email processing (replaces in-memory queue)
        var scheduledCount = 0;
        foreach (var metadata in metadataList)
        {
            try
            {
                // Determine priority based on email age
                var priority = DeterminePriority(metadata);

                // Schedule background job for processing
                _backgroundJobClient.Enqueue<EmailProcessingJob>(
                    job => job.ProcessEmailAsync(metadata.Id, emailAccount.Id, priority));

                scheduledCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule processing for email {EmailMetadataId}", metadata.Id);
            }
        }

        _logger.LogInformation("Scanned account {EmailAccountId}: {ScheduledCount} emails scheduled for processing",
            emailAccount.Id, scheduledCount);

        return Result.Success(emailList.Count);
    }

    private static EmailProcessingPriority DeterminePriority(EmailMetadata metadata)
    {
        // Recent emails get higher priority
        var age = DateTime.UtcNow - metadata.ReceivedAt;

        if (age < TimeSpan.FromHours(24))
            return EmailProcessingPriority.High;
        if (age < TimeSpan.FromDays(7))
            return EmailProcessingPriority.Normal;

        return EmailProcessingPriority.Low;
    }

    public async Task<Result<int>> ScanUserEmailAccountsAsync(
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
            return Result.Success(0);
        }

        _logger.LogInformation("Found {Count} active email accounts for user {UserId}",
            accountList.Count, userId);

        // Scan each account in parallel for better performance
        var scanTasks = accountList.Select(async account =>
        {
            try
            {
                var scanResult = await ScanEmailAccountAsync(account, null, cancellationToken);
                if (scanResult.IsSuccess)
                {
                    _logger.LogInformation("Successfully scanned account {AccountId}: {Count} emails",
                        account.Id, scanResult.Value);
                    return scanResult.Value;
                }
                else
                {
                    _logger.LogWarning("Failed to scan account {AccountId}: {Error}",
                        account.Id, scanResult.ErrorMessage);
                    return 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception scanning account {AccountId}", account.Id);
                return 0;
            }
        });

        // Wait for all scans to complete
        var results = await Task.WhenAll(scanTasks);
        var totalEmails = results.Sum();

        _logger.LogInformation("Completed email scan for user {UserId}. Total emails: {TotalEmails}",
            userId, totalEmails);

        return Result.Success(totalEmails);
    }
}

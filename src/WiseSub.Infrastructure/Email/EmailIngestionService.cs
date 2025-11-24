using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WiseSub.Application.Common.Configuration;
using WiseSub.Application.Common.Extensions;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;
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
    private readonly IEmailProviderFactory _providerFactory;
    private readonly IEmailQueueService _emailQueueService;
    private readonly IEmailMetadataService _emailMetadataService;
    private readonly EmailScanConfiguration _config;

    public EmailIngestionService(
        ILogger<EmailIngestionService> logger,
        IEmailAccountRepository emailAccountRepository,
        IEmailProviderFactory providerFactory,
        IEmailQueueService emailQueueService,
        IEmailMetadataService emailMetadataService,
        IOptions<EmailScanConfiguration> config)
    {
        _logger = logger;
        _emailAccountRepository = emailAccountRepository;
        _providerFactory = providerFactory;
        _emailQueueService = emailQueueService;
        _emailMetadataService = emailMetadataService;
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

        // Step 2: Queue emails for processing
        var emailsDict = emailList.ToDictionary(e => e.Id, e => e);
        var queueResult = await _emailQueueService.QueueEmailBatchAsync(
            metadataList,
            emailsDict,
            EmailProcessingPriority.High,
            cancellationToken);

        if (!queueResult.IsSuccess)
        {
            _logger.LogWarning("Failed to queue emails for account {EmailAccountId}: {Error}",
                emailAccount.Id, queueResult.ErrorMessage);
            return Result.Failure<int>(EmailMetadataErrors.QueueFailed);
        }

        _logger.LogInformation("Scanned account {EmailAccountId}: {QueuedCount} new emails queued",
            emailAccount.Id, queueResult.Value);

        return Result.Success(emailList.Count);
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

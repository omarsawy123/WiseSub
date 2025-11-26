using Hangfire;
using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;

namespace WiseSub.Infrastructure.BackgroundServices.Jobs;

/// <summary>
/// Background job that scans emails for subscription information.
/// Scheduled to run every 15 minutes per email account.
/// </summary>
public class EmailScanningJob
{
    private readonly ILogger<EmailScanningJob> _logger;
    private readonly IEmailAccountRepository _emailAccountRepository;
    private readonly IEmailIngestionService _emailIngestionService;

    public EmailScanningJob(
        ILogger<EmailScanningJob> logger,
        IEmailAccountRepository emailAccountRepository,
        IEmailIngestionService emailIngestionService)
    {
        _logger = logger;
        _emailAccountRepository = emailAccountRepository;
        _emailIngestionService = emailIngestionService;
    }

    /// <summary>
    /// Scans emails for all active email accounts and queues them for processing.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task ScanAllAccountsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting email scanning job for all accounts");

        try
        {
            // Get all active email accounts
            var emailAccounts = await _emailAccountRepository.GetAllActiveAsync(cancellationToken);
            var accountCount = 0;

            foreach (var account in emailAccounts)
            {
                try
                {
                    // Schedule individual account scan as a separate job
                    BackgroundJob.Enqueue<EmailScanningJob>(
                        job => job.ScanSingleAccountAsync(account.Id, CancellationToken.None));
                    
                    accountCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to schedule scan for email account {AccountId}", account.Id);
                }
            }

            _logger.LogInformation("Scheduled email scanning for {AccountCount} accounts", accountCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in email scanning job");
            throw;
        }
    }

    /// <summary>
    /// Scans emails for a specific email account.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task ScanSingleAccountAsync(string emailAccountId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting email scan for account {AccountId}", emailAccountId);

        try
        {
            var account = await _emailAccountRepository.GetByIdAsync(emailAccountId, cancellationToken);
            
            if (account == null)
            {
                _logger.LogWarning("Email account {AccountId} not found", emailAccountId);
                return;
            }

            if (!account.IsActive)
            {
                _logger.LogInformation("Email account {AccountId} is not active, skipping scan", emailAccountId);
                return;
            }

            // Perform the email scan - scan since last scan time
            var result = await _emailIngestionService.ScanEmailAccountAsync(
                account,
                account.LastScanAt,
                cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Completed email scan for account {AccountId}: {EmailsQueued} emails queued",
                    emailAccountId,
                    result.Value);
                
                // Update last scan time
                await _emailAccountRepository.UpdateLastScanAsync(emailAccountId, DateTime.UtcNow, cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "Email scan for account {AccountId} completed with issues: {Error}",
                    emailAccountId,
                    result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning emails for account {AccountId}", emailAccountId);
            throw;
        }
    }
}

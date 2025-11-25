using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Enums;

namespace WiseSub.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that processes emails from the queue
/// Handles classification, extraction, and subscription creation
/// </summary>
public class EmailProcessorService
{
    private readonly ILogger<EmailProcessorService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private Task? _executingTask;
    private CancellationTokenSource? _stoppingCts;

    public EmailProcessorService(
        ILogger<EmailProcessorService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Email Processor Service starting");
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executingTask = ExecuteAsync(_stoppingCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null)
            return;

        _logger.LogInformation("Email Processor Service stopping");
        
        _stoppingCts?.Cancel();
        
        await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
    }

    protected async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Processor Service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var queueService = scope.ServiceProvider.GetRequiredService<IEmailQueueService>();
                var metadataService = scope.ServiceProvider.GetRequiredService<IEmailMetadataService>();
                var aiService = scope.ServiceProvider.GetRequiredService<IAIExtractionService>();

                // Efficiently wait for an email to be available (uses Channel.WaitToReadAsync instead of polling)
                var hasEmail = await queueService.WaitForEmailAsync(stoppingToken);
                if (!hasEmail)
                {
                    continue; // Cancelled or no email available
                }

                // Dequeue next email
                var queuedEmail = await queueService.DequeueNextEmailAsync(stoppingToken);

                if (queuedEmail == null)
                {
                    // Race condition: another processor took the email, continue waiting
                    continue;
                }

                // Process the email
                await ProcessEmailAsync(
                    queuedEmail,
                    metadataService,
                    aiService,
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in email processor service main loop");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("Email Processor Service stopping");
    }

    private async Task ProcessEmailAsync(
        QueuedEmail queuedEmail,
        IEmailMetadataService metadataService,
        IAIExtractionService aiService,
        CancellationToken cancellationToken)
    {
        var emailMetadataId = queuedEmail.EmailMetadataId;

        try
        {
            _logger.LogInformation(
                "Processing email {EmailMetadataId} from sender {Sender}",
                emailMetadataId, queuedEmail.Email.Sender);

            // Update status to Processing
            await metadataService.UpdateStatusAsync(
                emailMetadataId,
                EmailProcessingStatus.Processing,
                cancellationToken);

            // Step 1: Classify email
            var classificationResult = await aiService.ClassifyEmailAsync(
                queuedEmail.Email,
                cancellationToken);

            if (!classificationResult.IsSuccess || classificationResult.Value == null)
            {
                _logger.LogWarning(
                    "Classification failed for email {EmailMetadataId}",
                    emailMetadataId);

                await metadataService.UpdateStatusAsync(
                    emailMetadataId,
                    EmailProcessingStatus.Failed,
                    cancellationToken);
                return;
            }

            var classification = classificationResult.Value;

            _logger.LogInformation(
                "Email {EmailMetadataId} classified: IsSubscription={IsSubscription}, Confidence={Confidence:F2}",
                emailMetadataId, classification.IsSubscriptionRelated, classification.Confidence);

            // If not subscription-related, mark as completed
            if (!classification.IsSubscriptionRelated)
            {
                await metadataService.MarkAsProcessedAsync(
                    emailMetadataId,
                    subscriptionId: null,
                    cancellationToken);
                return;
            }

            // Step 2: Extract subscription data
            var extractionResult = await aiService.ExtractSubscriptionDataAsync(
                queuedEmail.Email,
                cancellationToken);

            if (!extractionResult.IsSuccess || extractionResult.Value == null)
            {
                _logger.LogWarning(
                    "Extraction failed for email {EmailMetadataId}",
                    emailMetadataId);

                await metadataService.UpdateStatusAsync(
                    emailMetadataId,
                    EmailProcessingStatus.Failed,
                    cancellationToken);
                return;
            }

            var extraction = extractionResult.Value;

            _logger.LogInformation(
                "Extracted subscription from email {EmailMetadataId}: {ServiceName}, {Price} {Currency}",
                emailMetadataId, extraction.ServiceName, extraction.Price, extraction.Currency);

            // Step 3: Create subscription (TODO: Implement SubscriptionService)
            // For now, just mark as completed
            // In future: var subscription = await subscriptionService.CreateFromExtractionAsync(...)

            await metadataService.MarkAsProcessedAsync(
                emailMetadataId,
                subscriptionId: null,  // TODO: Use actual subscription ID
                cancellationToken);

            _logger.LogInformation(
                "Successfully processed email {EmailMetadataId}",
                emailMetadataId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process email {EmailMetadataId}",
                emailMetadataId);

            try
            {
                await metadataService.UpdateStatusAsync(
                    emailMetadataId,
                    EmailProcessingStatus.Failed,
                    cancellationToken);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx,
                    "Failed to update status to Failed for email {EmailMetadataId}",
                    emailMetadataId);
            }
        }
    }
}

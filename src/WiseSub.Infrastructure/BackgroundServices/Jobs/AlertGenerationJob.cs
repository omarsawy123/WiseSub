using Hangfire;
using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Infrastructure.BackgroundServices.Jobs;

/// <summary>
/// Background job that generates renewal alerts for upcoming subscriptions.
/// Scheduled to run daily to create 7-day and 3-day renewal alerts.
/// </summary>
public class AlertGenerationJob
{
    private readonly ILogger<AlertGenerationJob> _logger;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IAlertRepository _alertRepository;
    private readonly IUserRepository _userRepository;

    // Alert thresholds in days
    private const int SevenDayAlertThreshold = 7;
    private const int ThreeDayAlertThreshold = 3;

    public AlertGenerationJob(
        ILogger<AlertGenerationJob> logger,
        ISubscriptionRepository subscriptionRepository,
        IAlertRepository alertRepository,
        IUserRepository userRepository)
    {
        _logger = logger;
        _subscriptionRepository = subscriptionRepository;
        _alertRepository = alertRepository;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Generates renewal alerts for all users.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task GenerateAlertsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting alert generation job");

        try
        {
            // Get all users
            var users = await _userRepository.GetAllAsync(cancellationToken);
            var totalAlerts = 0;

            foreach (var user in users)
            {
                try
                {
                    var alertsGenerated = await GenerateAlertsForUserAsync(user.Id, cancellationToken);
                    totalAlerts += alertsGenerated;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate alerts for user {UserId}", user.Id);
                }
            }

            _logger.LogInformation("Alert generation completed. Total alerts generated: {AlertCount}", totalAlerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in alert generation job");
            throw;
        }
    }

    /// <summary>
    /// Generates renewal alerts for a specific user.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task<int> GenerateAlertsForUserAsync(string userId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Generating alerts for user {UserId}", userId);

        var alertsGenerated = 0;

        // Get subscriptions with upcoming renewals in the next 7 days
        var upcomingRenewals = await _subscriptionRepository.GetUpcomingRenewalsAsync(
            userId, 
            SevenDayAlertThreshold, 
            cancellationToken);

        foreach (var subscription in upcomingRenewals)
        {
            if (!subscription.NextRenewalDate.HasValue)
                continue;

            var daysUntilRenewal = (subscription.NextRenewalDate.Value.Date - DateTime.UtcNow.Date).Days;

            // Generate 7-day alert
            if (daysUntilRenewal <= SevenDayAlertThreshold && daysUntilRenewal > ThreeDayAlertThreshold)
            {
                var alert = await CreateRenewalAlertIfNotExistsAsync(
                    subscription,
                    AlertType.RenewalUpcoming7Days,
                    $"Your {subscription.ServiceName} subscription renews in {daysUntilRenewal} days",
                    cancellationToken);

                if (alert != null)
                    alertsGenerated++;
            }
            // Generate 3-day alert
            else if (daysUntilRenewal <= ThreeDayAlertThreshold && daysUntilRenewal >= 0)
            {
                var alert = await CreateRenewalAlertIfNotExistsAsync(
                    subscription,
                    AlertType.RenewalUpcoming3Days,
                    $"Your {subscription.ServiceName} subscription renews in {daysUntilRenewal} days",
                    cancellationToken);

                if (alert != null)
                    alertsGenerated++;
            }
        }

        _logger.LogDebug("Generated {AlertCount} alerts for user {UserId}", alertsGenerated, userId);
        return alertsGenerated;
    }

    /// <summary>
    /// Creates a renewal alert if one doesn't already exist for this subscription and alert type.
    /// </summary>
    private async Task<Alert?> CreateRenewalAlertIfNotExistsAsync(
        Subscription subscription,
        AlertType alertType,
        string message,
        CancellationToken cancellationToken)
    {
        // Check if an alert already exists for this subscription and type
        var existingAlert = await _alertRepository.GetBySubscriptionAndTypeAsync(
            subscription.Id,
            alertType,
            cancellationToken);

        if (existingAlert != null)
        {
            _logger.LogDebug(
                "Alert already exists for subscription {SubscriptionId} type {AlertType}",
                subscription.Id,
                alertType);
            return null;
        }

        // Create new alert
        var alert = new Alert
        {
            UserId = subscription.UserId,
            SubscriptionId = subscription.Id,
            Type = alertType,
            Message = message,
            Status = AlertStatus.Pending,
            ScheduledFor = DateTime.UtcNow
        };

        await _alertRepository.AddAsync(alert, cancellationToken);

        _logger.LogInformation(
            "Created {AlertType} alert for subscription {SubscriptionId} ({ServiceName})",
            alertType,
            subscription.Id,
            subscription.ServiceName);

        return alert;
    }
}

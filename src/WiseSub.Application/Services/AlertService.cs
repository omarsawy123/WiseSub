using System.Text.Json;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Services;

/// <summary>
/// Service for generating, managing, and delivering alerts for subscription events
/// </summary>
public class AlertService : IAlertService
{
    private readonly IAlertRepository _alertRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IUserRepository _userRepository;
    
    // Alert timing constants
    private const int RenewalWarning7Days = 7;
    private const int RenewalWarning3Days = 3;
    private const int TrialEndingWarningDays = 3;
    private const int UnusedSubscriptionMonths = 6;
    private const int MaxRetryCount = 3;

    public AlertService(
        IAlertRepository alertRepository,
        ISubscriptionRepository subscriptionRepository,
        IUserRepository userRepository)
    {
        _alertRepository = alertRepository;
        _subscriptionRepository = subscriptionRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<IEnumerable<Alert>>> GenerateRenewalAlertsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<IEnumerable<Alert>>(UserErrors.NotFound);
        }

        var preferences = GetPreferences(user);
        if (!preferences.EnableRenewalAlerts)
        {
            return Result.Success<IEnumerable<Alert>>(Enumerable.Empty<Alert>());
        }

        var activeSubscriptions = await _subscriptionRepository.GetByUserIdAndStatusAsync(
            userId, SubscriptionStatus.Active, cancellationToken);

        var alerts = new List<Alert>();
        var now = DateTime.UtcNow;

        foreach (var subscription in activeSubscriptions.Where(s => s.NextRenewalDate.HasValue))
        {
            var renewalDate = subscription.NextRenewalDate!.Value;
            var daysUntilRenewal = (renewalDate.Date - now.Date).Days;

            // Generate 7-day warning
            if (daysUntilRenewal <= RenewalWarning7Days && daysUntilRenewal > RenewalWarning3Days)
            {
                var alert = await CreateRenewalAlertIfNotExistsAsync(
                    subscription, AlertType.RenewalUpcoming7Days, daysUntilRenewal, cancellationToken);
                if (alert != null)
                {
                    alerts.Add(alert);
                }
            }
            // Generate 3-day warning
            else if (daysUntilRenewal <= RenewalWarning3Days && daysUntilRenewal >= 0)
            {
                var alert = await CreateRenewalAlertIfNotExistsAsync(
                    subscription, AlertType.RenewalUpcoming3Days, daysUntilRenewal, cancellationToken);
                if (alert != null)
                {
                    alerts.Add(alert);
                }
            }
        }

        return Result.Success<IEnumerable<Alert>>(alerts);
    }

    public async Task<Result<IEnumerable<Alert>>> GeneratePriceChangeAlertsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<IEnumerable<Alert>>(UserErrors.NotFound);
        }

        var preferences = GetPreferences(user);
        if (!preferences.EnablePriceChangeAlerts)
        {
            return Result.Success<IEnumerable<Alert>>(Enumerable.Empty<Alert>());
        }

        var subscriptions = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        var alerts = new List<Alert>();

        foreach (var subscription in subscriptions.Where(s => s.History.Any()))
        {
            // Look for recent price changes in history
            var priceChanges = subscription.History
                .Where(h => h.ChangeType == "Price" && h.ChangedAt >= DateTime.UtcNow.AddDays(-7))
                .OrderByDescending(h => h.ChangedAt)
                .ToList();

            foreach (var priceChange in priceChanges)
            {
                if (decimal.TryParse(priceChange.OldValue, out var oldPrice) &&
                    decimal.TryParse(priceChange.NewValue, out var newPrice) &&
                    newPrice > oldPrice)
                {
                    var existingAlert = await _alertRepository.GetBySubscriptionAndTypeAsync(
                        subscription.Id, AlertType.PriceIncrease, cancellationToken);

                    if (existingAlert == null || existingAlert.Status == AlertStatus.Sent)
                    {
                        var priceIncrease = newPrice - oldPrice;
                        var percentageIncrease = Math.Round((priceIncrease / oldPrice) * 100, 2);
                        
                        var alert = new Alert
                        {
                            UserId = userId,
                            SubscriptionId = subscription.Id,
                            Type = AlertType.PriceIncrease,
                            Message = $"Price increased for {subscription.ServiceName}: {subscription.Currency} {oldPrice:F2} → {subscription.Currency} {newPrice:F2} (+{percentageIncrease}%)",
                            ScheduledFor = DateTime.UtcNow,
                            Status = AlertStatus.Pending
                        };

                        await _alertRepository.AddAsync(alert, cancellationToken);
                        alerts.Add(alert);
                    }
                }
            }
        }

        return Result.Success<IEnumerable<Alert>>(alerts);
    }

    public async Task<Result<IEnumerable<Alert>>> GenerateTrialEndingAlertsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<IEnumerable<Alert>>(UserErrors.NotFound);
        }

        var preferences = GetPreferences(user);
        if (!preferences.EnableTrialEndingAlerts)
        {
            return Result.Success<IEnumerable<Alert>>(Enumerable.Empty<Alert>());
        }

        // Get subscriptions in trial status
        var trialSubscriptions = await _subscriptionRepository.GetByUserIdAndStatusAsync(
            userId, SubscriptionStatus.TrialActive, cancellationToken);

        var alerts = new List<Alert>();
        var now = DateTime.UtcNow;

        foreach (var subscription in trialSubscriptions.Where(s => s.NextRenewalDate.HasValue))
        {
            var trialEndDate = subscription.NextRenewalDate!.Value;
            var daysUntilTrialEnd = (trialEndDate.Date - now.Date).Days;

            if (daysUntilTrialEnd <= TrialEndingWarningDays && daysUntilTrialEnd >= 0)
            {
                var existingAlert = await _alertRepository.GetBySubscriptionAndTypeAsync(
                    subscription.Id, AlertType.TrialEnding, cancellationToken);

                if (existingAlert == null || existingAlert.Status == AlertStatus.Sent)
                {
                    var alert = new Alert
                    {
                        UserId = userId,
                        SubscriptionId = subscription.Id,
                        Type = AlertType.TrialEnding,
                        Message = daysUntilTrialEnd == 0 
                            ? $"Trial ending TODAY for {subscription.ServiceName}! Full price: {subscription.Currency} {subscription.Price:F2}/{subscription.BillingCycle}"
                            : $"Trial ending in {daysUntilTrialEnd} day{(daysUntilTrialEnd == 1 ? "" : "s")} for {subscription.ServiceName}. Full price: {subscription.Currency} {subscription.Price:F2}/{subscription.BillingCycle}",
                        ScheduledFor = DateTime.UtcNow,
                        Status = AlertStatus.Pending
                    };

                    await _alertRepository.AddAsync(alert, cancellationToken);
                    alerts.Add(alert);
                }
            }
        }

        return Result.Success<IEnumerable<Alert>>(alerts);
    }

    public async Task<Result<IEnumerable<Alert>>> GenerateUnusedSubscriptionAlertsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<IEnumerable<Alert>>(UserErrors.NotFound);
        }

        var preferences = GetPreferences(user);
        if (!preferences.EnableUnusedSubscriptionAlerts)
        {
            return Result.Success<IEnumerable<Alert>>(Enumerable.Empty<Alert>());
        }

        var activeSubscriptions = await _subscriptionRepository.GetByUserIdAndStatusAsync(
            userId, SubscriptionStatus.Active, cancellationToken);

        var alerts = new List<Alert>();
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-UnusedSubscriptionMonths);

        foreach (var subscription in activeSubscriptions)
        {
            // Check if subscription has been unused for 6+ months
            var lastActivity = subscription.LastActivityEmailAt ?? subscription.CreatedAt;
            
            if (lastActivity < sixMonthsAgo)
            {
                var existingAlert = await _alertRepository.GetBySubscriptionAndTypeAsync(
                    subscription.Id, AlertType.UnusedSubscription, cancellationToken);

                // Only create new alert if none exists or last one was sent more than 30 days ago
                if (existingAlert == null || 
                    (existingAlert.Status == AlertStatus.Sent && existingAlert.SentAt < DateTime.UtcNow.AddDays(-30)))
                {
                    var monthsUnused = (int)Math.Floor((DateTime.UtcNow - lastActivity).TotalDays / 30);
                    var monthlyPrice = NormalizeToMonthly(subscription.Price, subscription.BillingCycle);
                    var wastedAmount = monthlyPrice * monthsUnused;
                    
                    var alert = new Alert
                    {
                        UserId = userId,
                        SubscriptionId = subscription.Id,
                        Type = AlertType.UnusedSubscription,
                        Message = $"{subscription.ServiceName} appears unused for {monthsUnused} months. Potential savings: {subscription.Currency} {wastedAmount:F2}. Consider canceling?",
                        ScheduledFor = DateTime.UtcNow,
                        Status = AlertStatus.Pending
                    };

                    await _alertRepository.AddAsync(alert, cancellationToken);
                    alerts.Add(alert);
                }
            }
        }

        return Result.Success<IEnumerable<Alert>>(alerts);
    }

    public async Task<Result<AlertGenerationSummary>> GenerateAllAlertsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<AlertGenerationSummary>(UserErrors.NotFound);
        }

        var summary = new AlertGenerationSummary();

        // Generate renewal alerts
        var renewalResult = await GenerateRenewalAlertsAsync(userId, cancellationToken);
        if (renewalResult.IsSuccess)
        {
            summary.RenewalAlertsGenerated = renewalResult.Value.Count();
        }

        // Generate price change alerts
        var priceChangeResult = await GeneratePriceChangeAlertsAsync(userId, cancellationToken);
        if (priceChangeResult.IsSuccess)
        {
            summary.PriceChangeAlertsGenerated = priceChangeResult.Value.Count();
        }

        // Generate trial ending alerts
        var trialResult = await GenerateTrialEndingAlertsAsync(userId, cancellationToken);
        if (trialResult.IsSuccess)
        {
            summary.TrialEndingAlertsGenerated = trialResult.Value.Count();
        }

        // Generate unused subscription alerts
        var unusedResult = await GenerateUnusedSubscriptionAlertsAsync(userId, cancellationToken);
        if (unusedResult.IsSuccess)
        {
            summary.UnusedSubscriptionAlertsGenerated = unusedResult.Value.Count();
        }

        return Result.Success(summary);
    }

    public async Task<Result<IEnumerable<Alert>>> GetUserAlertsAsync(
        string userId,
        AlertStatus? statusFilter = null,
        AlertType? typeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<IEnumerable<Alert>>(UserErrors.NotFound);
        }

        var alerts = await _alertRepository.GetByUserIdAsync(userId, cancellationToken);

        if (statusFilter.HasValue)
        {
            alerts = alerts.Where(a => a.Status == statusFilter.Value);
        }

        if (typeFilter.HasValue)
        {
            alerts = alerts.Where(a => a.Type == typeFilter.Value);
        }

        return Result.Success<IEnumerable<Alert>>(alerts.OrderByDescending(a => a.ScheduledFor));
    }

    public async Task<Result<IEnumerable<Alert>>> GetPendingAlertsAsync(
        DateTime asOf,
        CancellationToken cancellationToken = default)
    {
        var alerts = await _alertRepository.GetPendingAlertsAsync(asOf, cancellationToken);
        return Result.Success(alerts);
    }

    public async Task<Result> MarkAlertAsSentAsync(
        string alertId,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken);
        if (alert == null)
        {
            return Result.Failure(AlertErrors.NotFound);
        }

        await _alertRepository.MarkAsSentAsync(alertId, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> MarkAlertAsFailedAsync(
        string alertId,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken);
        if (alert == null)
        {
            return Result.Failure(AlertErrors.NotFound);
        }

        alert.RetryCount++;
        
        if (alert.RetryCount >= MaxRetryCount)
        {
            await _alertRepository.MarkAsFailedAsync(alertId, cancellationToken);
        }
        else
        {
            // Reschedule with exponential backoff
            alert.ScheduledFor = DateTime.UtcNow.AddMinutes(Math.Pow(2, alert.RetryCount) * 5);
            await _alertRepository.UpdateAsync(alert, cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> SnoozeAlertAsync(
        string alertId,
        int snoozeHours = 24,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken);
        if (alert == null)
        {
            return Result.Failure(AlertErrors.NotFound);
        }

        alert.Status = AlertStatus.Snoozed;
        alert.ScheduledFor = DateTime.UtcNow.AddHours(snoozeHours);
        
        await _alertRepository.UpdateAsync(alert, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DismissAlertAsync(
        string alertId,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken);
        if (alert == null)
        {
            return Result.Failure(AlertErrors.NotFound);
        }

        await _alertRepository.DeleteAsync(alert, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<UserPreferences>> GetUserPreferencesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<UserPreferences>(UserErrors.NotFound);
        }

        var preferences = GetPreferences(user);
        return Result.Success(preferences);
    }

    public async Task<Result> UpdateUserPreferencesAsync(
        string userId,
        UserPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        user.PreferencesJson = JsonSerializer.Serialize(preferences);
        await _userRepository.UpdateAsync(user, cancellationToken);
        
        return Result.Success();
    }

    public async Task<Result<IEnumerable<Alert>>> GetTodaysAlertsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<IEnumerable<Alert>>(UserErrors.NotFound);
        }

        var alerts = await _alertRepository.GetTodaysAlertsAsync(userId, cancellationToken);
        return Result.Success(alerts);
    }

    public async Task<Result<Alert>> CreateAlertAsync(
        CreateAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        // Verify user exists
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<Alert>(UserErrors.NotFound);
        }

        // Verify subscription exists
        var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId, cancellationToken);
        if (subscription == null)
        {
            return Result.Failure<Alert>(SubscriptionErrors.NotFound);
        }

        // Check for duplicate alert
        var existingAlert = await _alertRepository.GetBySubscriptionAndTypeAsync(
            request.SubscriptionId, request.Type, cancellationToken);

        if (existingAlert != null && existingAlert.Status == AlertStatus.Pending)
        {
            return Result.Failure<Alert>(AlertErrors.AlreadySent);
        }

        var alert = new Alert
        {
            UserId = request.UserId,
            SubscriptionId = request.SubscriptionId,
            Type = request.Type,
            Message = request.Message,
            ScheduledFor = request.ScheduledFor,
            Status = AlertStatus.Pending
        };

        await _alertRepository.AddAsync(alert, cancellationToken);
        return Result.Success(alert);
    }

    #region Private Helper Methods

    private async Task<Alert?> CreateRenewalAlertIfNotExistsAsync(
        Subscription subscription,
        AlertType alertType,
        int daysUntilRenewal,
        CancellationToken cancellationToken)
    {
        var existingAlert = await _alertRepository.GetBySubscriptionAndTypeAsync(
            subscription.Id, alertType, cancellationToken);

        if (existingAlert != null && existingAlert.Status == AlertStatus.Pending)
        {
            return null;
        }

        var dayText = daysUntilRenewal == 1 ? "day" : "days";
        var alert = new Alert
        {
            UserId = subscription.UserId,
            SubscriptionId = subscription.Id,
            Type = alertType,
            Message = daysUntilRenewal == 0
                ? $"{subscription.ServiceName} renews TODAY! Amount: {subscription.Currency} {subscription.Price:F2}"
                : $"{subscription.ServiceName} renews in {daysUntilRenewal} {dayText}. Amount: {subscription.Currency} {subscription.Price:F2}",
            ScheduledFor = DateTime.UtcNow,
            Status = AlertStatus.Pending
        };

        await _alertRepository.AddAsync(alert, cancellationToken);
        return alert;
    }

    private static UserPreferences GetPreferences(User user)
    {
        if (string.IsNullOrEmpty(user.PreferencesJson))
        {
            return new UserPreferences();
        }

        try
        {
            return JsonSerializer.Deserialize<UserPreferences>(user.PreferencesJson) ?? new UserPreferences();
        }
        catch
        {
            return new UserPreferences();
        }
    }

    private static decimal NormalizeToMonthly(decimal price, BillingCycle billingCycle)
    {
        return billingCycle switch
        {
            BillingCycle.Annual => Math.Round(price / 12, 2),
            BillingCycle.Quarterly => Math.Round(price / 3, 2),
            BillingCycle.Weekly => Math.Round(price * 4.33m, 2),
            BillingCycle.Monthly => price,
            BillingCycle.Unknown => price,
            _ => price
        };
    }

    #endregion
}

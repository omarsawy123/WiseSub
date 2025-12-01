using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Service interface for alert generation, management, and delivery
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Generates renewal alerts for subscriptions renewing within specified days
    /// Creates alerts for 7-day and 3-day warnings
    /// </summary>
    Task<Result<IEnumerable<Alert>>> GenerateRenewalAlertsAsync(
        string userId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates price change alerts by detecting price increases from subscription history
    /// </summary>
    Task<Result<IEnumerable<Alert>>> GeneratePriceChangeAlertsAsync(
        string userId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates trial ending alerts for subscriptions with trials ending within 3 days
    /// </summary>
    Task<Result<IEnumerable<Alert>>> GenerateTrialEndingAlertsAsync(
        string userId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates unused subscription alerts for subscriptions with no activity for 6+ months
    /// </summary>
    Task<Result<IEnumerable<Alert>>> GenerateUnusedSubscriptionAlertsAsync(
        string userId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates all alert types for a user based on their preferences
    /// </summary>
    Task<Result<AlertGenerationSummary>> GenerateAllAlertsAsync(
        string userId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all alerts for a user with optional filtering
    /// </summary>
    Task<Result<IEnumerable<Alert>>> GetUserAlertsAsync(
        string userId,
        AlertStatus? statusFilter = null,
        AlertType? typeFilter = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets pending alerts ready to be sent
    /// </summary>
    Task<Result<IEnumerable<Alert>>> GetPendingAlertsAsync(
        DateTime asOf,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks an alert as sent
    /// </summary>
    Task<Result> MarkAlertAsSentAsync(
        string alertId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks an alert as failed with retry logic
    /// </summary>
    Task<Result> MarkAlertAsFailedAsync(
        string alertId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Snoozes an alert for specified hours
    /// </summary>
    Task<Result> SnoozeAlertAsync(
        string alertId,
        int snoozeHours = 24,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Dismisses/deletes an alert
    /// </summary>
    Task<Result> DismissAlertAsync(
        string alertId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets user's alert preferences
    /// </summary>
    Task<Result<UserPreferences>> GetUserPreferencesAsync(
        string userId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates user's alert preferences
    /// </summary>
    Task<Result> UpdateUserPreferencesAsync(
        string userId,
        UserPreferences preferences,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets today's alerts for a user (for daily digest)
    /// </summary>
    Task<Result<IEnumerable<Alert>>> GetTodaysAlertsAsync(
        string userId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a single alert
    /// </summary>
    Task<Result<Alert>> CreateAlertAsync(
        CreateAlertRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request model for creating an alert
/// </summary>
public class CreateAlertRequest
{
    public required string UserId { get; init; }
    public required string SubscriptionId { get; init; }
    public required AlertType Type { get; init; }
    public required string Message { get; init; }
    public required DateTime ScheduledFor { get; init; }
}

/// <summary>
/// Summary of alert generation operation
/// </summary>
public class AlertGenerationSummary
{
    public int RenewalAlertsGenerated { get; set; }
    public int PriceChangeAlertsGenerated { get; set; }
    public int TrialEndingAlertsGenerated { get; set; }
    public int UnusedSubscriptionAlertsGenerated { get; set; }
    public int TotalAlertsGenerated => RenewalAlertsGenerated + PriceChangeAlertsGenerated + 
                                        TrialEndingAlertsGenerated + UnusedSubscriptionAlertsGenerated;
    public int SkippedDueToDuplicates { get; set; }
    public int SkippedDueToPreferences { get; set; }
}

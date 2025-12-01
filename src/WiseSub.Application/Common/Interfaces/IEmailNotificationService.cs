using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Service interface for sending email notifications via SendGrid
/// </summary>
public interface IEmailNotificationService
{
    /// <summary>
    /// Sends a single alert notification email
    /// </summary>
    Task<Result<EmailDeliveryResult>> SendAlertAsync(
        Alert alert,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends multiple alert notifications in a batch
    /// </summary>
    Task<Result<BatchDeliveryResult>> SendBatchAlertsAsync(
        IEnumerable<Alert> alerts,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a daily digest email containing all alerts for a user
    /// </summary>
    Task<Result<EmailDeliveryResult>> SendDailyDigestAsync(
        string userId,
        IEnumerable<Alert> alerts,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the delivery status of a previously sent email
    /// </summary>
    Task<Result<DeliveryStatus>> GetDeliveryStatusAsync(
        string messageId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates email configuration and connectivity
    /// </summary>
    Task<Result<bool>> ValidateConfigurationAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a single email delivery attempt
/// </summary>
public class EmailDeliveryResult
{
    public required string MessageId { get; init; }
    public required bool Success { get; init; }
    public DeliveryStatus Status { get; init; } = DeliveryStatus.Pending;
    public DateTime SentAt { get; init; } = DateTime.UtcNow;
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; }
}

/// <summary>
/// Result of a batch email delivery operation
/// </summary>
public class BatchDeliveryResult
{
    public int TotalAttempted { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<EmailDeliveryResult> Results { get; set; } = new();
    
    public bool AllSucceeded => FailureCount == 0 && TotalAttempted > 0;
    public double SuccessRate => TotalAttempted > 0 
        ? (double)SuccessCount / TotalAttempted * 100 
        : 0;
}

/// <summary>
/// Delivery status of an email
/// </summary>
public enum DeliveryStatus
{
    Pending,
    Sent,
    Delivered,
    Opened,
    Clicked,
    Bounced,
    Failed,
    Unknown
}

/// <summary>
/// Email template data for alert notifications
/// </summary>
public class AlertEmailData
{
    public required string RecipientEmail { get; init; }
    public required string RecipientName { get; init; }
    public required string Subject { get; init; }
    public required AlertEmailContent Content { get; init; }
}

/// <summary>
/// Content for a single alert email
/// </summary>
public class AlertEmailContent
{
    public required string AlertType { get; init; }
    public required string ServiceName { get; init; }
    public string? ServiceLogoUrl { get; init; }
    public required string Message { get; init; }
    public DateTime? RenewalDate { get; init; }
    public decimal? CurrentPrice { get; init; }
    public decimal? PreviousPrice { get; init; }
    public string? Currency { get; init; }
    public string? CancellationLink { get; init; }
    public string? DashboardLink { get; init; }
}

/// <summary>
/// Content for daily digest email
/// </summary>
public class DailyDigestData
{
    public required string RecipientEmail { get; init; }
    public required string RecipientName { get; init; }
    public required DateTime DigestDate { get; init; }
    public required List<DigestAlertItem> Alerts { get; init; }
    public required DigestSummary Summary { get; init; }
    public string? DashboardLink { get; init; }
}

/// <summary>
/// Individual alert item in daily digest
/// </summary>
public class DigestAlertItem
{
    public required string AlertType { get; init; }
    public required string ServiceName { get; init; }
    public required string Message { get; init; }
    public DateTime? ActionDate { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
}

/// <summary>
/// Summary section for daily digest
/// </summary>
public class DigestSummary
{
    public int TotalAlerts { get; set; }
    public int RenewalAlerts { get; set; }
    public int PriceChangeAlerts { get; set; }
    public int TrialEndingAlerts { get; set; }
    public int UnusedSubscriptionAlerts { get; set; }
    public decimal? TotalUpcomingRenewals { get; set; }
    public string? Currency { get; init; }
}

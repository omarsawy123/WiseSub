using WiseSub.Domain.Enums;

namespace WiseSub.Domain.Entities;

public class Alert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public AlertType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.Pending;
    public int RetryCount { get; set; }
    
    // Navigation properties
    public User User { get; set; } = null!;
    public Subscription Subscription { get; set; } = null!;
}

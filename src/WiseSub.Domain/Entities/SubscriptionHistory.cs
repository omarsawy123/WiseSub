namespace WiseSub.Domain.Entities;

public class SubscriptionHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SubscriptionId { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string ChangeType { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string? SourceEmailId { get; set; }
    
    // Navigation properties
    public Subscription Subscription { get; set; } = null!;
}

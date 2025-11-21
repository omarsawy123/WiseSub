using WiseSub.Domain.Enums;

namespace WiseSub.Domain.Entities;

public class Subscription
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string EmailAccountId { get; set; } = string.Empty;
    
    // Core subscription data
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public BillingCycle BillingCycle { get; set; }
    public DateTime? NextRenewalDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    
    // Metadata
    public string? VendorId { get; set; }
    public string? CancellationLink { get; set; }
    public bool RequiresUserReview { get; set; }
    public double ExtractionConfidence { get; set; }
    
    // Tracking
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
    public DateTime? LastActivityEmailAt { get; set; }
    
    // Navigation properties
    public User User { get; set; } = null!;
    public EmailAccount EmailAccount { get; set; } = null!;
    public VendorMetadata? Vendor { get; set; }
    public ICollection<SubscriptionHistory> History { get; set; } = new List<SubscriptionHistory>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}

namespace WiseSub.Domain.Entities;

public class EmailMetadata
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EmailAccountId { get; set; } = string.Empty;
    public string ExternalEmailId { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? SubscriptionId { get; set; }
    
    // Navigation properties
    public EmailAccount EmailAccount { get; set; } = null!;
}

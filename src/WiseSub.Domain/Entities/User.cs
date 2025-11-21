using WiseSub.Domain.Enums;

namespace WiseSub.Domain.Entities;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OAuthProvider { get; set; } = string.Empty;
    public string OAuthSubjectId { get; set; } = string.Empty;
    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string PreferencesJson { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<EmailAccount> EmailAccounts { get; set; } = new List<EmailAccount>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}

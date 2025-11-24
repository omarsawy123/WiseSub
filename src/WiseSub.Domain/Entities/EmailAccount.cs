using System.ComponentModel.DataAnnotations.Schema;
using WiseSub.Domain.Enums;

namespace WiseSub.Domain.Entities;

public class EmailAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public EmailProvider Provider { get; set; }
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiresAt { get; set; }
    public DateTime LastScanAt { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Generic provider sync metadata for incremental sync support
    /// Key examples: "gmail_history_id", "outlook_delta_token", etc.
    /// </summary>
    [NotMapped] // Will be serialized to JSON column in database migration
    public Dictionary<string, string> ProviderSyncMetadata { get; set; } = new();
    
    /// <summary>
    /// Legacy Gmail-specific history ID. Use ProviderSyncMetadata instead.
    /// </summary>
    [Obsolete("Use ProviderSyncMetadata with key 'gmail_history_id' instead")]
    public string? GmailHistoryId { get; set; }
    
    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<EmailMetadata> EmailMetadata { get; set; } = new List<EmailMetadata>();
}

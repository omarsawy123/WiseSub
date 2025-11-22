namespace WiseSub.Application.Common.Models;

/// <summary>
/// Represents an email that has been queued for processing
/// </summary>
public class QueuedEmail
{
    /// <summary>
    /// The email metadata ID
    /// </summary>
    public string EmailMetadataId { get; set; } = string.Empty;

    /// <summary>
    /// The email account ID
    /// </summary>
    public string EmailAccountId { get; set; } = string.Empty;

    /// <summary>
    /// The email message
    /// </summary>
    public EmailMessage Email { get; set; } = null!;

    /// <summary>
    /// Processing priority
    /// </summary>
    public EmailProcessingPriority Priority { get; set; }

    /// <summary>
    /// When the email was queued
    /// </summary>
    public DateTime QueuedAt { get; set; }
}

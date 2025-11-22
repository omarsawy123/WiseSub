namespace WiseSub.Application.Common.Models;

/// <summary>
/// Represents an email message retrieved from an email provider
/// </summary>
public class EmailMessage
{
    public string Id { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public string? FolderId { get; set; }
    public string? FolderName { get; set; }
}

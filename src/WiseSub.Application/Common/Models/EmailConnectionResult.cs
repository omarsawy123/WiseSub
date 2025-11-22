namespace WiseSub.Application.Common.Models;

/// <summary>
/// Result of email account connection operation
/// </summary>
public class EmailConnectionResult
{
    public bool Success { get; set; }
    public string? EmailAccountId { get; set; }
    public string? EmailAddress { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
}

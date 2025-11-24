namespace WiseSub.Application.Common.Configuration;

/// <summary>
/// Configuration for email scanning operations
/// </summary>
public class EmailScanConfiguration
{
    public const string SectionName = "EmailScan";
    
    /// <summary>
    /// Subject keywords to filter subscription-related emails
    /// </summary>
    public List<string> SubjectKeywords { get; set; } = new()
    {
        "subscription", "renewal", "invoice", "receipt", 
        "payment", "billing", "charge", "trial", "upgrade",
        "membership", "plan", "premium", "pro", "plus"
    };
    
    /// <summary>
    /// Default number of months to look back when scanning (if not specified)
    /// </summary>
    public int DefaultLookbackMonths { get; set; } = 12;
    
    /// <summary>
    /// Maximum number of emails to retrieve per scan to prevent overload
    /// </summary>
    public int MaxEmailsPerScan { get; set; } = 500;
    
    /// <summary>
    /// Maximum number of email accounts to scan concurrently
    /// </summary>
    public int MaxConcurrentAccountScans { get; set; } = 5;
}

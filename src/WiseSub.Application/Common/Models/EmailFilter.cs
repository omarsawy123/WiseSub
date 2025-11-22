namespace WiseSub.Application.Common.Models;

/// <summary>
/// Filter criteria for retrieving emails
/// </summary>
public class EmailFilter
{
    /// <summary>
    /// Retrieve emails after this date
    /// </summary>
    public DateTime? Since { get; set; }
    
    /// <summary>
    /// Retrieve emails before this date
    /// </summary>
    public DateTime? Before { get; set; }
    
    /// <summary>
    /// Filter by sender domain (e.g., "netflix.com")
    /// </summary>
    public List<string> SenderDomains { get; set; } = new();
    
    /// <summary>
    /// Filter by subject keywords
    /// </summary>
    public List<string> SubjectKeywords { get; set; } = new();
    
    /// <summary>
    /// Filter by folder names (e.g., "Purchases", "Receipts")
    /// </summary>
    public List<string> FolderNames { get; set; } = new();
    
    /// <summary>
    /// Maximum number of emails to retrieve
    /// </summary>
    public int? MaxResults { get; set; }
}

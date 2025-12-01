namespace WiseSub.Application.Common.Configuration;

/// <summary>
/// Configuration for email notifications via SendGrid
/// </summary>
public class EmailNotificationConfiguration
{
    public const string SectionName = "SendGrid";
    
    /// <summary>
    /// SendGrid API key for authentication
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Sender email address (must be verified in SendGrid)
    /// </summary>
    public string SenderEmail { get; set; } = "notifications@wisesub.app";
    
    /// <summary>
    /// Sender name displayed in emails
    /// </summary>
    public string SenderName { get; set; } = "WiseSub Notifications";
    
    /// <summary>
    /// Maximum number of retry attempts for failed sends
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// Initial delay in milliseconds before first retry (doubles each attempt)
    /// </summary>
    public int InitialRetryDelayMs { get; set; } = 1000;
    
    /// <summary>
    /// Whether to enable email sending (can be disabled for development)
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Sandbox mode - emails are validated but not actually sent
    /// </summary>
    public bool SandboxMode { get; set; } = false;
    
    /// <summary>
    /// Base URL for the application (used in email links)
    /// </summary>
    public string ApplicationBaseUrl { get; set; } = "https://app.wisesub.app";
    
    /// <summary>
    /// Maximum emails per batch send operation
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;
    
    /// <summary>
    /// Template IDs for different alert types (optional - uses default templates if not specified)
    /// </summary>
    public EmailTemplateIds Templates { get; set; } = new();
}

/// <summary>
/// SendGrid template IDs for different alert types
/// </summary>
public class EmailTemplateIds
{
    /// <summary>
    /// Template ID for renewal reminder emails
    /// </summary>
    public string? RenewalReminder { get; set; }
    
    /// <summary>
    /// Template ID for price change notification emails
    /// </summary>
    public string? PriceChange { get; set; }
    
    /// <summary>
    /// Template ID for trial ending reminder emails
    /// </summary>
    public string? TrialEnding { get; set; }
    
    /// <summary>
    /// Template ID for unused subscription notification emails
    /// </summary>
    public string? UnusedSubscription { get; set; }
    
    /// <summary>
    /// Template ID for daily digest emails
    /// </summary>
    public string? DailyDigest { get; set; }
}

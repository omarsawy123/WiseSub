namespace WiseSub.Application.Common.Models;

/// <summary>
/// Represents the current status of the email processing queue
/// </summary>
public class QueueStatus
{
    /// <summary>
    /// Total number of emails pending processing
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// Number of high priority emails in queue
    /// </summary>
    public int HighPriorityCount { get; set; }

    /// <summary>
    /// Number of normal priority emails in queue
    /// </summary>
    public int NormalPriorityCount { get; set; }

    /// <summary>
    /// Number of low priority emails in queue
    /// </summary>
    public int LowPriorityCount { get; set; }

    /// <summary>
    /// Total number of emails processed
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// Timestamp of the status
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

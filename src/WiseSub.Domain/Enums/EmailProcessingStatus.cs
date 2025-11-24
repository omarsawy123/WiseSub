namespace WiseSub.Domain.Enums;

/// <summary>
/// Represents the processing status of an email in the pipeline
/// </summary>
public enum EmailProcessingStatus
{
    /// <summary>
    /// Email metadata created but not yet queued
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Email is in the queue waiting to be processed
    /// </summary>
    Queued = 1,
    
    /// <summary>
    /// Email is currently being processed
    /// </summary>
    Processing = 2,
    
    /// <summary>
    /// Email has been successfully processed
    /// </summary>
    Completed = 3,
    
    /// <summary>
    /// Email processing failed after retries
    /// </summary>
    Failed = 4
}

using WiseSub.Application.Common.Models;

namespace WiseSub.Application.Common.Interfaces;

/// <summary>
/// Service for AI-powered extraction of subscription data from emails
/// </summary>
public interface IAIExtractionService
{
    /// <summary>
    /// Classifies whether an email is subscription-related
    /// </summary>
    /// <param name="email">The email message to classify</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Classification result with confidence score</returns>
    Task<ClassificationResult> ClassifyEmailAsync(
        EmailMessage email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts structured subscription data from an email
    /// </summary>
    /// <param name="email">The email message to extract from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extraction result with subscription data and confidence scores</returns>
    Task<ExtractionResult> ExtractSubscriptionDataAsync(
        EmailMessage email,
        CancellationToken cancellationToken = default);
}

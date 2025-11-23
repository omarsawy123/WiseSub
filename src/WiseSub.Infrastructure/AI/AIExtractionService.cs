using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;
using WiseSub.Domain.Enums;

namespace WiseSub.Infrastructure.AI;

/// <summary>
/// AI-powered extraction service for subscription data from emails
/// </summary>
public class AIExtractionService : IAIExtractionService
{
    private readonly IOpenAIClient _openAIClient;
    private readonly ILogger<AIExtractionService> _logger;

    // Confidence thresholds as per design document
    private const double HighConfidenceThreshold = 0.85;
    private const double MediumConfidenceThreshold = 0.60;

    public AIExtractionService(
        IOpenAIClient openAIClient,
        ILogger<AIExtractionService> logger)
    {
        _openAIClient = openAIClient;
        _logger = logger;
    }

    public async Task<Result<ClassificationResult>> ClassifyEmailAsync(
        EmailMessage email,
        CancellationToken cancellationToken = default)
    {
        if (email == null)
            return Result.Failure<ClassificationResult>(EmailMetadataErrors.InvalidFormat);

        _logger.LogDebug("Classifying email from {Sender} with subject: {Subject}", 
            email.Sender, email.Subject);

        var systemPrompt = GetClassificationSystemPrompt();
        var userPrompt = GetClassificationUserPrompt(email);

        var response = await _openAIClient.GetJsonCompletionAsync<ClassificationResponse>(
            systemPrompt,
            userPrompt,
            temperature: 0.1,
            cancellationToken);

        if (response == null)
        {
            _logger.LogWarning("Received null response from OpenAI for classification");
            return Result.Failure<ClassificationResult>(EmailMetadataErrors.ProcessingFailed);
        }

        var result = new ClassificationResult
        {
            IsSubscriptionRelated = response.IsSubscriptionRelated,
            Confidence = response.Confidence,
            EmailType = response.EmailType,
            Reason = response.Reason
        };

        _logger.LogInformation(
            "Email classified as {IsSubscriptionRelated} with confidence {Confidence:F2}",
            result.IsSubscriptionRelated, result.Confidence);

        return Result.Success(result);
    }

    public async Task<Result<ExtractionResult>> ExtractSubscriptionDataAsync(
        EmailMessage email,
        CancellationToken cancellationToken = default)
    {
        if (email == null)
            return Result.Failure<ExtractionResult>(EmailMetadataErrors.InvalidFormat);

        _logger.LogDebug("Extracting subscription data from email: {Subject}", email.Subject);

        var systemPrompt = GetExtractionSystemPrompt();
        var userPrompt = GetExtractionUserPrompt(email);

        var response = await _openAIClient.GetJsonCompletionAsync<ExtractionResponse>(
            systemPrompt,
            userPrompt,
            temperature: 0.1,
            cancellationToken);

        if (response == null)
        {
            _logger.LogWarning("Received null response from OpenAI for extraction");
            return Result.Failure<ExtractionResult>(EmailMetadataErrors.ProcessingFailed);
        }

        var result = MapExtractionResponse(response);

        _logger.LogInformation(
            "Extracted subscription: {ServiceName}, Price: {Price} {Currency}, Confidence: {Confidence:F2}",
            result.ServiceName, result.Price, result.Currency, result.ConfidenceScore);

        return Result.Success(result);
    }

    private string GetClassificationSystemPrompt()
    {
        return @"You are an expert at analyzing emails to determine if they are related to recurring subscriptions or services.

Your task is to classify emails into subscription-related or not subscription-related.

Subscription-related emails include:
- Purchase receipts for subscription services
- Renewal notices
- Welcome emails for new subscriptions
- Free trial confirmations
- Price change notifications
- Subscription confirmation emails
- Billing statements for recurring services

NOT subscription-related:
- One-time purchases
- General marketing emails
- Shipping notifications for physical products
- Account security notifications (unless about subscription)
- General service updates (unless about billing/renewal)

Respond with a JSON object containing:
- isSubscriptionRelated (boolean): true if the email is subscription-related
- confidence (number): confidence score from 0.0 to 1.0
- emailType (string): type of email (e.g., ""purchase_receipt"", ""renewal_notice"", ""trial_confirmation"", ""price_change"", ""welcome"", ""other"")
- reason (string): brief explanation of the classification

Be conservative - only classify as subscription-related if you're reasonably confident.";
    }

    private string GetClassificationUserPrompt(EmailMessage email)
    {
        // Truncate body to avoid token limits (keep first 2000 characters)
        var body = email.Body.Length > 2000 
            ? email.Body.Substring(0, 2000) + "..." 
            : email.Body;

        return $@"Classify this email:

From: {email.Sender}
Subject: {email.Subject}
Date: {email.ReceivedAt:yyyy-MM-dd}

Body:
{body}

Respond with JSON only.";
    }

    private string GetExtractionSystemPrompt()
    {
        return @"You are an expert at extracting structured subscription information from emails.

Extract the following information:
- serviceName: Name of the service/subscription (e.g., ""Netflix"", ""Spotify Premium"", ""Adobe Creative Cloud"")
- price: Numeric price value (e.g., 9.99)
- currency: ISO currency code (e.g., ""USD"", ""EUR"", ""GBP"")
- billingCycle: One of: ""Weekly"", ""Monthly"", ""Quarterly"", ""Annual"", ""Unknown""
- nextRenewalDate: Next renewal date in ISO format (YYYY-MM-DD) if available, null otherwise
- category: Category of service (e.g., ""Entertainment"", ""Productivity"", ""Utilities"", ""Software"", ""Gaming"", ""Education"", ""Health"", ""Other"")
- cancellationLink: URL for cancellation if present in email, null otherwise
- fieldConfidences: Object with confidence scores (0.0-1.0) for each field

For each field, provide a confidence score indicating how certain you are about the extracted value.
If a field cannot be determined with reasonable confidence, use null and set confidence to 0.0.

Respond with a JSON object containing all these fields.

Language support: Handle emails in English, German, French, and Spanish.

Examples of billing cycle extraction:
- ""monthly subscription"" → ""Monthly""
- ""billed annually"" → ""Annual""
- ""every 3 months"" → ""Quarterly""
- ""per week"" → ""Weekly""
- ""one-time"" → ""Unknown""

Examples of category:
- Netflix, Spotify, Disney+ → ""Entertainment""
- Microsoft 365, Adobe → ""Productivity""
- Electricity, Internet → ""Utilities""
- GitHub, AWS → ""Software""
- Xbox Game Pass → ""Gaming""
- Coursera, Udemy → ""Education""
- Gym membership → ""Health""";
    }

    private string GetExtractionUserPrompt(EmailMessage email)
    {
        // Truncate body to avoid token limits (keep first 3000 characters for extraction)
        var body = email.Body.Length > 3000 
            ? email.Body.Substring(0, 3000) + "..." 
            : email.Body;

        return $@"Extract subscription information from this email:

From: {email.Sender}
Subject: {email.Subject}
Date: {email.ReceivedAt:yyyy-MM-dd}

Body:
{body}

Respond with JSON only.";
    }

    private ExtractionResult MapExtractionResponse(ExtractionResponse response)
    {
        var result = new ExtractionResult
        {
            ServiceName = response.ServiceName ?? string.Empty,
            Price = response.Price,
            Currency = response.Currency ?? "USD",
            BillingCycle = ParseBillingCycle(response.BillingCycle),
            NextRenewalDate = response.NextRenewalDate,
            Category = response.Category ?? "Other",
            CancellationLink = response.CancellationLink,
            FieldConfidences = response.FieldConfidences ?? new Dictionary<string, double>()
        };

        // Calculate overall confidence score
        result.ConfidenceScore = CalculateOverallConfidence(result.FieldConfidences);

        // Determine if requires user review based on confidence thresholds
        result.RequiresUserReview = result.ConfidenceScore < MediumConfidenceThreshold;

        // Add warnings for missing critical fields
        if (string.IsNullOrWhiteSpace(result.ServiceName))
        {
            result.Warnings.Add("Service name could not be determined");
        }
        if (result.Price <= 0)
        {
            result.Warnings.Add("Price could not be determined or is invalid");
        }
        if (result.BillingCycle == BillingCycle.Unknown)
        {
            result.Warnings.Add("Billing cycle could not be determined");
        }
        if (!result.NextRenewalDate.HasValue)
        {
            result.Warnings.Add("Next renewal date could not be determined");
        }

        return result;
    }

    private BillingCycle ParseBillingCycle(string? billingCycle)
    {
        if (string.IsNullOrWhiteSpace(billingCycle))
            return BillingCycle.Unknown;

        return billingCycle.ToLowerInvariant() switch
        {
            "weekly" => BillingCycle.Weekly,
            "monthly" => BillingCycle.Monthly,
            "quarterly" => BillingCycle.Quarterly,
            "annual" or "annually" or "yearly" => BillingCycle.Annual,
            _ => BillingCycle.Unknown
        };
    }

    private double CalculateOverallConfidence(Dictionary<string, double> fieldConfidences)
    {
        if (fieldConfidences == null || fieldConfidences.Count == 0)
            return 0.0;

        // Weight critical fields more heavily
        var weights = new Dictionary<string, double>
        {
            { "serviceName", 0.25 },
            { "price", 0.25 },
            { "billingCycle", 0.20 },
            { "nextRenewalDate", 0.15 },
            { "category", 0.10 },
            { "currency", 0.05 }
        };

        double totalWeight = 0.0;
        double weightedSum = 0.0;

        foreach (var kvp in fieldConfidences)
        {
            var fieldName = kvp.Key;
            var confidence = kvp.Value;

            if (weights.TryGetValue(fieldName, out var weight))
            {
                weightedSum += confidence * weight;
                totalWeight += weight;
            }
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 0.0;
    }

    private ExtractionResult CreateFailedExtractionResult(string reason)
    {
        return new ExtractionResult
        {
            ServiceName = string.Empty,
            Price = 0,
            Currency = "USD",
            BillingCycle = BillingCycle.Unknown,
            ConfidenceScore = 0.0,
            RequiresUserReview = true,
            Warnings = new List<string> { reason }
        };
    }

    // Internal DTOs for JSON deserialization
    private class ClassificationResponse
    {
        [JsonPropertyName("isSubscriptionRelated")]
        public bool IsSubscriptionRelated { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("emailType")]
        public string? EmailType { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    private class ExtractionResponse
    {
        [JsonPropertyName("serviceName")]
        public string? ServiceName { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("billingCycle")]
        public string? BillingCycle { get; set; }

        [JsonPropertyName("nextRenewalDate")]
        public DateTime? NextRenewalDate { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("cancellationLink")]
        public string? CancellationLink { get; set; }

        [JsonPropertyName("fieldConfidences")]
        public Dictionary<string, double>? FieldConfidences { get; set; }
    }
}

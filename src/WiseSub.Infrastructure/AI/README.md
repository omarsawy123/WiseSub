# AI Extraction Service

## Overview

The AI Extraction Service uses OpenAI's GPT models to intelligently classify and extract subscription data from emails. This service is a critical component of the WiseSub platform, enabling automatic discovery and tracking of user subscriptions.

## Architecture

The AI extraction functionality is split into two main components following the **Single Responsibility Principle**:

### 1. OpenAIClient (`OpenAIClient.cs`)

**Responsibility**: Low-level communication with OpenAI API

**Features**:
- Sends completion requests to OpenAI
- Handles JSON-formatted responses
- Implements retry logic with exponential backoff
- Manages rate limiting and transient errors
- Validates inputs and handles errors gracefully

**Configuration**:
```json
{
  "OpenAI": {
    "ApiKey": "YOUR_OPENAI_API_KEY",
    "Model": "gpt-4o-mini",
    "MaxRetries": 3,
    "InitialRetryDelayMs": 1000
  }
}
```

### 2. AIExtractionService (`AIExtractionService.cs`)

**Responsibility**: High-level business logic for email classification and data extraction

**Features**:
- Email classification (subscription-related or not)
- Structured data extraction (service name, price, billing cycle, etc.)
- Confidence scoring with weighted field importance
- Multi-language support (English, German, French, Spanish)
- Automatic flagging of low-confidence extractions for user review

## Usage

### Email Classification

Determines if an email is subscription-related:

```csharp
var classificationResult = await _aiExtractionService.ClassifyEmailAsync(email);

if (classificationResult.IsSuccess && classificationResult.Value.IsSubscriptionRelated)
{
    // Process as subscription email
    if (classificationResult.Value.Confidence >= 0.85)
    {
        // High confidence - proceed with extraction
    }
}
```

**Classification Response**:
```json
{
  "isSubscriptionRelated": true,
  "confidence": 0.92,
  "emailType": "purchase_receipt",
  "reason": "Email contains subscription purchase confirmation with recurring billing details"
}
```

### Subscription Data Extraction

Extracts structured subscription information:

```csharp
var extractionResult = await _aiExtractionService.ExtractSubscriptionDataAsync(email);

if (extractionResult.IsSuccess)
{
    var data = extractionResult.Value;
    
    if (data.RequiresUserReview)
    {
        // Low confidence - flag for user review
    }
    else
    {
        // High confidence - create subscription automatically
    }
}
```

**Extraction Response**:
```json
{
  "serviceName": "Netflix Premium",
  "price": 15.99,
  "currency": "USD",
  "billingCycle": "Monthly",
  "nextRenewalDate": "2025-12-22",
  "category": "Entertainment",
  "cancellationLink": "https://netflix.com/cancel",
  "confidenceScore": 0.89,
  "requiresUserReview": false,
  "fieldConfidences": {
    "serviceName": 0.95,
    "price": 0.92,
    "billingCycle": 0.88,
    "nextRenewalDate": 0.85,
    "category": 0.90,
    "currency": 0.98
  },
  "warnings": []
}
```

## Confidence Scoring

### Thresholds

- **High Confidence**: ≥ 0.85 - Automatic processing
- **Medium Confidence**: 0.60 - 0.84 - Automatic processing with user notification
- **Low Confidence**: < 0.60 - Requires user review

### Weighted Field Importance

The overall confidence score is calculated using weighted averages:

| Field | Weight | Importance |
|-------|--------|------------|
| Service Name | 25% | Critical - identifies the subscription |
| Price | 25% | Critical - core billing information |
| Billing Cycle | 20% | High - determines renewal frequency |
| Next Renewal Date | 15% | Medium - helps with alerts |
| Category | 10% | Low - for organization only |
| Currency | 5% | Low - usually determinable from context |

**Formula**:
```
Overall Confidence = Σ(Field Confidence × Field Weight) / Total Weight
```

## Error Handling

### Retry Logic

The OpenAI client implements exponential backoff for retryable errors:

- **Initial Delay**: 1000ms (configurable)
- **Max Retries**: 3 (configurable)
- **Backoff Strategy**: Exponential (1s → 2s → 4s)

**Retryable Errors**:
- Rate limiting (429)
- Service unavailable (503)
- Network timeouts
- Transient HTTP errors

**Non-Retryable Errors**:
- Invalid API key (401)
- Invalid request format (400)
- Model not found (404)

### Error Responses

All methods return `Result<T>` for explicit error handling:

```csharp
var result = await _aiExtractionService.ClassifyEmailAsync(email);

if (result.IsFailure)
{
    _logger.LogError("Classification failed: {Error}", result.ErrorMessage);
    // Handle error appropriately
}
```

## Multi-Language Support

The service supports emails in multiple languages:

- **English** (primary)
- **German** (Deutsch)
- **French** (Français)
- **Spanish** (Español)

The prompts are designed to handle language variations automatically without requiring language detection.

## Performance Considerations

### Token Optimization

- Email bodies are truncated to 2000 characters for classification
- Email bodies are truncated to 3000 characters for extraction
- This reduces API costs while maintaining accuracy

### Caching (Future Enhancement)

Consider implementing caching for:
- Repeated emails (same external ID)
- Similar email patterns from the same sender
- Vendor metadata lookups

## Testing

### Unit Tests

Test the AI extraction service with mocked OpenAI client:

```csharp
var mockOpenAIClient = new Mock<IOpenAIClient>();
mockOpenAIClient
    .Setup(x => x.GetJsonCompletionAsync<ClassificationResponse>(
        It.IsAny<string>(), 
        It.IsAny<string>(), 
        It.IsAny<double>(), 
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(new ClassificationResponse 
    { 
        IsSubscriptionRelated = true, 
        Confidence = 0.92 
    });

var service = new AIExtractionService(mockOpenAIClient.Object, logger);
```

### Integration Tests

Test with real OpenAI API (requires API key):

```csharp
// Use test configuration with real API key
var service = serviceProvider.GetRequiredService<IAIExtractionService>();

var testEmail = new EmailMessage
{
    Sender = "billing@netflix.com",
    Subject = "Your Netflix subscription has been renewed",
    Body = "Your monthly Netflix Premium subscription..."
};

var result = await service.ClassifyEmailAsync(testEmail);
Assert.True(result.IsSuccess);
Assert.True(result.Value.IsSubscriptionRelated);
```

## Monitoring and Logging

### Key Metrics to Track

1. **Classification Accuracy**: % of correctly classified emails
2. **Extraction Accuracy**: % of correctly extracted fields
3. **Confidence Distribution**: Distribution of confidence scores
4. **API Latency**: Average response time from OpenAI
5. **Error Rate**: % of failed API calls
6. **Retry Rate**: % of requests requiring retries

### Logging Levels

- **Debug**: API request/response details
- **Information**: Classification/extraction results
- **Warning**: Retries, low confidence results
- **Error**: API failures, deserialization errors

## Cost Optimization

### Token Usage

- **Classification**: ~500-800 tokens per email
- **Extraction**: ~800-1200 tokens per email
- **Model**: gpt-4o-mini (cost-effective for this use case)

### Estimated Costs (as of 2025)

With gpt-4o-mini pricing:
- Input: $0.15 per 1M tokens
- Output: $0.60 per 1M tokens

**Per Email**:
- Classification: ~$0.0001
- Extraction: ~$0.0002
- **Total**: ~$0.0003 per email

**For 1000 emails**: ~$0.30

## Future Enhancements

1. **Fine-tuning**: Train a custom model on user-corrected data
2. **Batch Processing**: Process multiple emails in a single API call
3. **Streaming**: Use streaming responses for faster perceived performance
4. **Fallback Models**: Use cheaper models for high-confidence cases
5. **Local Models**: Consider local LLMs for privacy-sensitive deployments
6. **Active Learning**: Learn from user corrections to improve prompts

## Troubleshooting

### Common Issues

**Issue**: "OpenAI API key not configured"
- **Solution**: Set `OpenAI:ApiKey` in appsettings.json or environment variables

**Issue**: Rate limiting errors (429)
- **Solution**: Increase `InitialRetryDelayMs` or reduce concurrent requests

**Issue**: Low extraction accuracy
- **Solution**: Review and refine system prompts, increase model temperature slightly

**Issue**: High API costs
- **Solution**: Implement caching, reduce email body truncation length, or use a cheaper model

## References

- [OpenAI API Documentation](https://platform.openai.com/docs)
- [OpenAI .NET SDK](https://github.com/openai/openai-dotnet)
- [Clean Architecture Principles](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Result Pattern](https://enterprisecraftsmanship.com/posts/error-handling-exception-or-result/)

## Support

For issues or questions about the AI extraction service:
1. Check the logs for detailed error messages
2. Verify OpenAI API key and configuration
3. Review the troubleshooting section above
4. Contact the development team

# AI Extraction Service

## Overview

The AI Extraction Service is a core component of the WiseSub subscription tracking system that uses OpenAI's GPT-4o-mini model to automatically extract subscription information from emails.

## Features

### Email Classification
- Determines if an email is subscription-related
- Provides confidence scores (0.0 to 1.0)
- Identifies email types (renewal_notice, purchase_receipt, trial_confirmation, etc.)
- Supports multiple languages (English, German, French, Spanish)

### Subscription Data Extraction
- Extracts service name
- Identifies price and currency
- Determines billing cycle (Weekly, Monthly, Quarterly, Annual)
- Finds next renewal date
- Categorizes subscriptions (Entertainment, Productivity, Software, etc.)
- Locates cancellation links
- Provides field-level confidence scores

### Confidence-Based Review Flagging
- High confidence (>0.85): Auto-creates subscription
- Medium confidence (0.60-0.85): Creates with review flag
- Low confidence (<0.60): Flags for user review

## Configuration

### appsettings.json

```json
{
  "OpenAI": {
    "ApiKey": "YOUR_OPENAI_API_KEY",
    "Model": "gpt-4o-mini"
  }
}
```

### Environment Variables (Production)

For production deployments, use environment variables or Azure Key Vault:

```bash
OpenAI__ApiKey=your-api-key-here
OpenAI__Model=gpt-4o-mini
```

## Usage

### Dependency Injection

The service is automatically registered in `DependencyInjection.cs`:

```csharp
services.AddSingleton<IOpenAIClient, OpenAIClient>();
services.AddScoped<IAIExtractionService, AIExtractionService>();
```

### Classifying Emails

```csharp
public class EmailProcessor
{
    private readonly IAIExtractionService _aiService;

    public EmailProcessor(IAIExtractionService aiService)
    {
        _aiService = aiService;
    }

    public async Task ProcessEmailAsync(EmailMessage email)
    {
        // Classify the email
        var classification = await _aiService.ClassifyEmailAsync(email);

        if (classification.IsSubscriptionRelated && classification.Confidence > 0.85)
        {
            // Extract subscription data
            var extraction = await _aiService.ExtractSubscriptionDataAsync(email);

            if (!extraction.RequiresUserReview)
            {
                // Create subscription automatically
                await CreateSubscriptionAsync(extraction);
            }
            else
            {
                // Flag for user review
                await FlagForReviewAsync(extraction);
            }
        }
    }
}
```

### Handling Extraction Results

```csharp
var result = await _aiService.ExtractSubscriptionDataAsync(email);

// Check confidence
if (result.ConfidenceScore >= 0.85)
{
    Console.WriteLine("High confidence extraction");
}
else if (result.ConfidenceScore >= 0.60)
{
    Console.WriteLine("Medium confidence - review recommended");
}
else
{
    Console.WriteLine("Low confidence - user review required");
}

// Check for warnings
if (result.Warnings.Any())
{
    foreach (var warning in result.Warnings)
    {
        Console.WriteLine($"Warning: {warning}");
    }
}

// Access extracted data
Console.WriteLine($"Service: {result.ServiceName}");
Console.WriteLine($"Price: {result.Price} {result.Currency}");
Console.WriteLine($"Billing: {result.BillingCycle}");
Console.WriteLine($"Next Renewal: {result.NextRenewalDate}");
```

## Error Handling

The service handles various error scenarios:

### API Errors
- Network failures
- Rate limiting
- Invalid API keys
- Timeout errors

### Data Errors
- Malformed JSON responses
- Missing required fields
- Invalid date formats
- Unsupported currencies

All errors are logged and wrapped in appropriate exceptions.

## Cost Optimization

### Token Usage
- Email bodies are truncated to 2000 characters for classification
- Email bodies are truncated to 3000 characters for extraction
- This reduces token costs while maintaining accuracy

### Model Selection
- Default model: `gpt-4o-mini` (cost-effective)
- Estimated cost: $0.10-0.20 per 1000 emails processed
- For 100 beta users processing ~2000 emails/month: $10-20/month

### Batch Processing
Consider implementing batch processing for cost optimization:
- Queue emails and process in batches
- Use background jobs to spread load
- Implement caching for similar emails

## Multi-Language Support

The service supports emails in:
- English
- German
- French
- Spanish

The AI model automatically detects the language and extracts information accordingly.

## Testing

### Unit Tests
Basic unit tests are provided in `WiseSub.Infrastructure.Tests/AI/AIExtractionServiceTests.cs`.

### Integration Testing
For full integration testing with actual OpenAI API calls:

1. Set up a test OpenAI API key
2. Create test email fixtures
3. Run extraction and verify results
4. Monitor token usage and costs

Example test email fixtures are recommended for:
- Netflix subscription renewal
- Spotify premium confirmation
- Adobe Creative Cloud annual billing
- Trial ending notifications
- Price change alerts

## Monitoring

### Key Metrics to Track
- Classification accuracy rate
- Extraction confidence scores
- API response times
- Token usage and costs
- Error rates by type

### Logging
The service logs:
- All classification attempts
- All extraction attempts
- Confidence scores
- Warnings and errors
- API call durations

## Troubleshooting

### Low Confidence Scores
- Check email content quality
- Verify email is in supported language
- Ensure email contains subscription information
- Review field-level confidence scores

### API Errors
- Verify API key is valid
- Check rate limits
- Monitor network connectivity
- Review error logs

### Incorrect Extractions
- Review the email content
- Check if information is clearly stated
- Consider adding to training examples
- Flag for manual review

## Future Enhancements

### Planned Improvements
- Custom fine-tuned models for better accuracy
- Support for additional languages
- Improved category classification
- Better handling of promotional emails
- Enhanced price change detection
- Automatic vendor matching

### Performance Optimizations
- Response caching for similar emails
- Batch API requests
- Parallel processing
- Smart retry logic

## Security Considerations

### API Key Management
- Never commit API keys to source control
- Use environment variables or Key Vault
- Rotate keys regularly
- Monitor API usage for anomalies

### Data Privacy
- Only email metadata is stored
- Full email content is never persisted
- Truncated content is sent to OpenAI
- Comply with GDPR and data protection laws

## Support

For issues or questions:
1. Check the logs for error details
2. Verify configuration settings
3. Test with sample emails
4. Review OpenAI API status
5. Contact the development team

## References

- [OpenAI API Documentation](https://platform.openai.com/docs)
- [GPT-4o-mini Model](https://platform.openai.com/docs/models/gpt-4o-mini)
- [WiseSub Design Document](./ARCHITECTURE.md)
- [Requirements Document](../.kiro/specs/subscription-tracker/requirements.md)

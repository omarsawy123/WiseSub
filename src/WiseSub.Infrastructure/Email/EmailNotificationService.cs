using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using WiseSub.Application.Common.Configuration;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;

namespace WiseSub.Infrastructure.Email;

/// <summary>
/// SendGrid-based implementation of email notifications
/// </summary>
public class EmailNotificationService : IEmailNotificationService
{
    private readonly ISendGridClient _sendGridClient;
    private readonly EmailNotificationConfiguration _config;
    private readonly IUserRepository _userRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILogger<EmailNotificationService> _logger;
    
    public EmailNotificationService(
        ISendGridClient sendGridClient,
        IOptions<EmailNotificationConfiguration> config,
        IUserRepository userRepository,
        ISubscriptionRepository subscriptionRepository,
        ILogger<EmailNotificationService> logger)
    {
        _sendGridClient = sendGridClient ?? throw new ArgumentNullException(nameof(sendGridClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _subscriptionRepository = subscriptionRepository ?? throw new ArgumentNullException(nameof(subscriptionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc />
    public async Task<Result<EmailDeliveryResult>> SendAlertAsync(
        Alert alert,
        CancellationToken cancellationToken = default)
    {
        if (alert == null)
            return Result.Failure<EmailDeliveryResult>(EmailNotificationErrors.InvalidAlert);
        
        if (!_config.Enabled)
        {
            _logger.LogInformation("Email notifications disabled. Skipping alert {AlertId}", alert.Id);
            return Result.Success(new EmailDeliveryResult
            {
                MessageId = $"disabled-{alert.Id}",
                Success = true,
                Status = DeliveryStatus.Sent,
                SentAt = DateTime.UtcNow
            });
        }
        
        var user = await _userRepository.GetByIdAsync(alert.UserId, cancellationToken);
        if (user == null)
            return Result.Failure<EmailDeliveryResult>(UserErrors.NotFound);
        
        var subscription = await _subscriptionRepository.GetByIdAsync(alert.SubscriptionId, cancellationToken);
        
        var emailData = BuildAlertEmailData(alert, user, subscription);
        
        return await SendWithRetryAsync(emailData, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<Result<BatchDeliveryResult>> SendBatchAlertsAsync(
        IEnumerable<Alert> alerts,
        CancellationToken cancellationToken = default)
    {
        if (alerts == null)
            return Result.Failure<BatchDeliveryResult>(EmailNotificationErrors.InvalidAlert);
        
        var alertList = alerts.ToList();
        if (alertList.Count == 0)
            return Result.Success(new BatchDeliveryResult
            {
                TotalAttempted = 0,
                SuccessCount = 0,
                FailureCount = 0
            });
        
        var result = new BatchDeliveryResult
        {
            TotalAttempted = alertList.Count
        };
        
        // Process in batches to respect SendGrid limits
        var batches = alertList
            .Select((alert, index) => new { alert, index })
            .GroupBy(x => x.index / _config.MaxBatchSize)
            .Select(g => g.Select(x => x.alert).ToList());
        
        foreach (var batch in batches)
        {
            foreach (var alert in batch)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                var sendResult = await SendAlertAsync(alert, cancellationToken);
                
                if (sendResult.IsSuccess)
                {
                    result.SuccessCount++;
                    result.Results.Add(sendResult.Value!);
                }
                else
                {
                    result.FailureCount++;
                    result.Results.Add(new EmailDeliveryResult
                    {
                        MessageId = $"failed-{alert.Id}",
                        Success = false,
                        Status = DeliveryStatus.Failed,
                        ErrorMessage = sendResult.ErrorMessage
                    });
                }
            }
        }
        
        _logger.LogInformation(
            "Batch email delivery complete. Total: {Total}, Success: {Success}, Failed: {Failed}",
            result.TotalAttempted, result.SuccessCount, result.FailureCount);
        
        return Result.Success(result);
    }
    
    /// <inheritdoc />
    public async Task<Result<EmailDeliveryResult>> SendDailyDigestAsync(
        string userId,
        IEnumerable<Alert> alerts,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<EmailDeliveryResult>(UserErrors.NotFound);
        
        if (alerts == null)
            return Result.Failure<EmailDeliveryResult>(EmailNotificationErrors.InvalidAlert);
        
        var alertList = alerts.ToList();
        if (alertList.Count == 0)
        {
            return Result.Success(new EmailDeliveryResult
            {
                MessageId = $"no-alerts-{userId}",
                Success = true,
                Status = DeliveryStatus.Sent,
                SentAt = DateTime.UtcNow
            });
        }
        
        if (!_config.Enabled)
        {
            _logger.LogInformation("Email notifications disabled. Skipping daily digest for user {UserId}", userId);
            return Result.Success(new EmailDeliveryResult
            {
                MessageId = $"disabled-digest-{userId}",
                Success = true,
                Status = DeliveryStatus.Sent,
                SentAt = DateTime.UtcNow
            });
        }
        
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            return Result.Failure<EmailDeliveryResult>(UserErrors.NotFound);
        
        var digestData = await BuildDailyDigestDataAsync(user, alertList, cancellationToken);
        var msg = BuildDailyDigestMessage(digestData);
        
        return await SendWithRetryAsync(msg, cancellationToken);
    }
    
    /// <inheritdoc />
    public Task<Result<DeliveryStatus>> GetDeliveryStatusAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return Task.FromResult(Result.Failure<DeliveryStatus>(EmailNotificationErrors.InvalidMessageId));
        
        // SendGrid doesn't provide a simple way to check individual message status via API
        // In production, you would use SendGrid's Event Webhook for real-time tracking
        // For now, return Unknown to indicate we don't have tracking data
        _logger.LogDebug("Delivery status check for message {MessageId} - returning Unknown", messageId);
        
        return Task.FromResult(Result.Success(DeliveryStatus.Unknown));
    }
    
    /// <inheritdoc />
    public async Task<Result<bool>> ValidateConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_config.ApiKey) || _config.ApiKey == "YOUR_SENDGRID_API_KEY")
        {
            _logger.LogWarning("SendGrid API key not configured");
            return Result.Failure<bool>(EmailNotificationErrors.InvalidConfiguration);
        }
        
        if (string.IsNullOrWhiteSpace(_config.SenderEmail))
        {
            _logger.LogWarning("SendGrid sender email not configured");
            return Result.Failure<bool>(EmailNotificationErrors.InvalidConfiguration);
        }
        
        // Attempt a validation by creating a simple message (without actually sending)
        try
        {
            var testMsg = new SendGridMessage
            {
                From = new EmailAddress(_config.SenderEmail, _config.SenderName)
            };
            testMsg.AddTo("test@example.com");
            testMsg.Subject = "Configuration Test";
            testMsg.PlainTextContent = "Test";
            
            // In sandbox mode, we can actually send to validate
            if (_config.SandboxMode)
            {
                testMsg.SetSandBoxMode(true);
                var response = await _sendGridClient.SendEmailAsync(testMsg, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Body.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("SendGrid validation failed: {StatusCode} - {Body}", 
                        response.StatusCode, body);
                    return Result.Failure<bool>(EmailNotificationErrors.ConfigurationValidationFailed);
                }
            }
            
            _logger.LogInformation("SendGrid configuration validated successfully");
            return Result.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendGrid configuration validation failed");
            return Result.Failure<bool>(EmailNotificationErrors.ConfigurationValidationFailed);
        }
    }
    
    private AlertEmailData BuildAlertEmailData(Alert alert, User user, Subscription? subscription)
    {
        var subject = GetAlertSubject(alert.Type, subscription?.ServiceName ?? "Subscription");
        
        return new AlertEmailData
        {
            RecipientEmail = user.Email,
            RecipientName = user.Name,
            Subject = subject,
            Content = new AlertEmailContent
            {
                AlertType = GetAlertTypeName(alert.Type),
                ServiceName = subscription?.ServiceName ?? "Unknown Service",
                ServiceLogoUrl = subscription?.Vendor?.LogoUrl,
                Message = alert.Message,
                RenewalDate = subscription?.NextRenewalDate,
                CurrentPrice = subscription?.Price,
                PreviousPrice = GetPreviousPrice(alert, subscription),
                Currency = subscription?.Currency ?? "USD",
                CancellationLink = subscription?.CancellationLink,
                DashboardLink = $"{_config.ApplicationBaseUrl}/dashboard"
            }
        };
    }
    
    private async Task<DailyDigestData> BuildDailyDigestDataAsync(
        User user, 
        List<Alert> alerts,
        CancellationToken cancellationToken)
    {
        var digestItems = new List<DigestAlertItem>();
        decimal totalRenewals = 0;
        string? currency = null;
        
        foreach (var alert in alerts)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(alert.SubscriptionId, cancellationToken);
            
            if (subscription != null)
            {
                currency ??= subscription.Currency;
                
                if (alert.Type == AlertType.RenewalUpcoming7Days || alert.Type == AlertType.RenewalUpcoming3Days)
                {
                    totalRenewals += subscription.Price;
                }
            }
            
            digestItems.Add(new DigestAlertItem
            {
                AlertType = GetAlertTypeName(alert.Type),
                ServiceName = subscription?.ServiceName ?? "Unknown Service",
                Message = alert.Message,
                ActionDate = subscription?.NextRenewalDate ?? alert.ScheduledFor,
                Amount = subscription?.Price,
                Currency = subscription?.Currency ?? "USD"
            });
        }
        
        var summary = new DigestSummary
        {
            TotalAlerts = alerts.Count,
            RenewalAlerts = alerts.Count(a => a.Type == AlertType.RenewalUpcoming7Days || a.Type == AlertType.RenewalUpcoming3Days),
            PriceChangeAlerts = alerts.Count(a => a.Type == AlertType.PriceIncrease),
            TrialEndingAlerts = alerts.Count(a => a.Type == AlertType.TrialEnding),
            UnusedSubscriptionAlerts = alerts.Count(a => a.Type == AlertType.UnusedSubscription),
            TotalUpcomingRenewals = totalRenewals > 0 ? totalRenewals : null,
            Currency = currency
        };
        
        return new DailyDigestData
        {
            RecipientEmail = user.Email,
            RecipientName = user.Name,
            DigestDate = DateTime.UtcNow.Date,
            Alerts = digestItems,
            Summary = summary,
            DashboardLink = $"{_config.ApplicationBaseUrl}/dashboard"
        };
    }
    
    private SendGridMessage BuildAlertMessage(AlertEmailData emailData)
    {
        var msg = new SendGridMessage();
        msg.SetFrom(new EmailAddress(_config.SenderEmail, _config.SenderName));
        msg.AddTo(new EmailAddress(emailData.RecipientEmail, emailData.RecipientName));
        msg.Subject = emailData.Subject;
        
        // Check if we have a template ID for this alert type
        var templateId = GetTemplateId(emailData.Content.AlertType);
        if (!string.IsNullOrEmpty(templateId))
        {
            msg.SetTemplateId(templateId);
            msg.SetTemplateData(new
            {
                recipient_name = emailData.RecipientName,
                alert_type = emailData.Content.AlertType,
                service_name = emailData.Content.ServiceName,
                service_logo = emailData.Content.ServiceLogoUrl,
                message = emailData.Content.Message,
                renewal_date = emailData.Content.RenewalDate?.ToString("MMMM dd, yyyy"),
                current_price = FormatPrice(emailData.Content.CurrentPrice, emailData.Content.Currency),
                previous_price = FormatPrice(emailData.Content.PreviousPrice, emailData.Content.Currency),
                cancellation_link = emailData.Content.CancellationLink,
                dashboard_link = emailData.Content.DashboardLink
            });
        }
        else
        {
            // Use default template
            msg.HtmlContent = BuildDefaultAlertHtml(emailData);
            msg.PlainTextContent = BuildDefaultAlertText(emailData);
        }
        
        if (_config.SandboxMode)
        {
            msg.SetSandBoxMode(true);
        }
        
        return msg;
    }
    
    private SendGridMessage BuildDailyDigestMessage(DailyDigestData digestData)
    {
        var msg = new SendGridMessage();
        msg.SetFrom(new EmailAddress(_config.SenderEmail, _config.SenderName));
        msg.AddTo(new EmailAddress(digestData.RecipientEmail, digestData.RecipientName));
        msg.Subject = $"WiseSub Daily Digest - {digestData.DigestDate:MMMM dd, yyyy}";
        
        if (!string.IsNullOrEmpty(_config.Templates.DailyDigest))
        {
            msg.SetTemplateId(_config.Templates.DailyDigest);
            msg.SetTemplateData(new
            {
                recipient_name = digestData.RecipientName,
                digest_date = digestData.DigestDate.ToString("MMMM dd, yyyy"),
                total_alerts = digestData.Summary.TotalAlerts,
                renewal_alerts = digestData.Summary.RenewalAlerts,
                price_change_alerts = digestData.Summary.PriceChangeAlerts,
                trial_ending_alerts = digestData.Summary.TrialEndingAlerts,
                unused_alerts = digestData.Summary.UnusedSubscriptionAlerts,
                total_upcoming = FormatPrice(digestData.Summary.TotalUpcomingRenewals, digestData.Summary.Currency),
                alerts = digestData.Alerts.Select(a => new
                {
                    type = a.AlertType,
                    service = a.ServiceName,
                    message = a.Message,
                    date = a.ActionDate?.ToString("MMM dd"),
                    amount = FormatPrice(a.Amount, a.Currency)
                }),
                dashboard_link = digestData.DashboardLink
            });
        }
        else
        {
            msg.HtmlContent = BuildDefaultDigestHtml(digestData);
            msg.PlainTextContent = BuildDefaultDigestText(digestData);
        }
        
        if (_config.SandboxMode)
        {
            msg.SetSandBoxMode(true);
        }
        
        return msg;
    }
    
    private async Task<Result<EmailDeliveryResult>> SendWithRetryAsync(
        AlertEmailData emailData,
        CancellationToken cancellationToken)
    {
        var msg = BuildAlertMessage(emailData);
        return await SendWithRetryAsync(msg, cancellationToken);
    }
    
    private async Task<Result<EmailDeliveryResult>> SendWithRetryAsync(
        SendGridMessage msg,
        CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var delayMs = _config.InitialRetryDelayMs;
        Exception? lastException = null;
        
        while (retryCount <= _config.MaxRetries)
        {
            try
            {
                var response = await _sendGridClient.SendEmailAsync(msg, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var messageId = response.Headers.Contains("X-Message-Id") 
                        ? response.Headers.GetValues("X-Message-Id").FirstOrDefault() 
                        : Guid.NewGuid().ToString();
                    
                    _logger.LogInformation("Email sent successfully. MessageId: {MessageId}", messageId);
                    
                    return Result.Success(new EmailDeliveryResult
                    {
                        MessageId = messageId ?? Guid.NewGuid().ToString(),
                        Success = true,
                        Status = DeliveryStatus.Sent,
                        SentAt = DateTime.UtcNow,
                        RetryCount = retryCount
                    });
                }
                
                var errorBody = await response.Body.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "SendGrid returned {StatusCode}. Attempt {Attempt}/{MaxRetries}. Body: {Body}",
                    response.StatusCode, retryCount + 1, _config.MaxRetries + 1, errorBody);
                
                // Check if we should retry based on status code
                var statusCode = (int)response.StatusCode;
                if (statusCode >= 400 && statusCode < 500 && statusCode != 429)
                {
                    // Client error (except rate limit) - don't retry
                    return Result.Failure<EmailDeliveryResult>(
                        new Error("Email.SendFailed", $"SendGrid error: {response.StatusCode} - {errorBody}"));
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, 
                    "SendGrid send failed. Attempt {Attempt}/{MaxRetries}", 
                    retryCount + 1, _config.MaxRetries + 1);
            }
            
            retryCount++;
            if (retryCount <= _config.MaxRetries)
            {
                await Task.Delay(delayMs, cancellationToken);
                delayMs *= 2; // Exponential backoff
            }
        }
        
        _logger.LogError(lastException, 
            "SendGrid send failed after {MaxRetries} retries", _config.MaxRetries);
        
        return Result.Failure<EmailDeliveryResult>(EmailNotificationErrors.SendFailed);
    }
    
    private string GetAlertSubject(AlertType alertType, string serviceName)
    {
        return alertType switch
        {
            AlertType.RenewalUpcoming7Days => $"⏰ {serviceName} renews in 7 days",
            AlertType.RenewalUpcoming3Days => $"⚠️ {serviceName} renews in 3 days",
            AlertType.PriceIncrease => $"💰 Price increase for {serviceName}",
            AlertType.TrialEnding => $"⏳ Your {serviceName} trial is ending soon",
            AlertType.UnusedSubscription => $"💤 You haven't used {serviceName} in a while",
            _ => $"WiseSub Alert: {serviceName}"
        };
    }
    
    private static string GetAlertTypeName(AlertType alertType)
    {
        return alertType switch
        {
            AlertType.RenewalUpcoming7Days => "Renewal Reminder (7 days)",
            AlertType.RenewalUpcoming3Days => "Renewal Reminder (3 days)",
            AlertType.PriceIncrease => "Price Change",
            AlertType.TrialEnding => "Trial Ending",
            AlertType.UnusedSubscription => "Unused Subscription",
            _ => "Alert"
        };
    }
    
    private string? GetTemplateId(string alertTypeName)
    {
        return alertTypeName switch
        {
            "Renewal Reminder (7 days)" => _config.Templates.RenewalReminder,
            "Renewal Reminder (3 days)" => _config.Templates.RenewalReminder,
            "Price Change" => _config.Templates.PriceChange,
            "Trial Ending" => _config.Templates.TrialEnding,
            "Unused Subscription" => _config.Templates.UnusedSubscription,
            _ => null
        };
    }
    
    private static decimal? GetPreviousPrice(Alert alert, Subscription? subscription)
    {
        if (alert.Type != AlertType.PriceIncrease || subscription == null)
            return null;
        
        // Try to extract previous price from message
        // Format: "Price increased from $X.XX to $Y.YY"
        var message = alert.Message;
        var fromIndex = message.IndexOf("from ", StringComparison.OrdinalIgnoreCase);
        var toIndex = message.IndexOf(" to ", StringComparison.OrdinalIgnoreCase);
        
        if (fromIndex >= 0 && toIndex > fromIndex)
        {
            var priceStr = message.Substring(fromIndex + 5, toIndex - fromIndex - 5)
                .Replace("$", "").Replace(",", "").Trim();
            if (decimal.TryParse(priceStr, out var price))
                return price;
        }
        
        return null;
    }
    
    private static string FormatPrice(decimal? price, string? currency)
    {
        if (!price.HasValue)
            return string.Empty;
        
        var symbol = currency?.ToUpperInvariant() switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            "JPY" => "¥",
            "CAD" => "CA$",
            "AUD" => "A$",
            _ => "$"
        };
        
        return $"{symbol}{price:N2}";
    }
    
    private string BuildDefaultAlertHtml(AlertEmailData emailData)
    {
        var content = emailData.Content;
        var priceSection = content.CurrentPrice.HasValue
            ? $@"<p style=""font-size: 18px; color: #2c3e50;"">
                   <strong>Amount:</strong> {FormatPrice(content.CurrentPrice, content.Currency)}
                   {(content.PreviousPrice.HasValue ? $" (was {FormatPrice(content.PreviousPrice, content.Currency)})" : "")}
                 </p>"
            : "";
        
        var renewalSection = content.RenewalDate.HasValue
            ? $@"<p style=""color: #7f8c8d;"">
                   <strong>Renewal Date:</strong> {content.RenewalDate:MMMM dd, yyyy}
                 </p>"
            : "";
        
        var cancellationSection = !string.IsNullOrEmpty(content.CancellationLink)
            ? $@"<p>
                   <a href=""{content.CancellationLink}"" style=""color: #e74c3c;"">Cancel Subscription</a>
                 </p>"
            : "";
        
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif; margin: 0; padding: 20px; background-color: #f5f6fa;"">
    <div style=""max-width: 600px; margin: 0 auto; background-color: white; border-radius: 8px; padding: 30px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);"">
        <div style=""text-align: center; margin-bottom: 30px;"">
            <h1 style=""color: #3498db; margin: 0;"">WiseSub</h1>
        </div>
        
        <h2 style=""color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px;"">{content.AlertType}</h2>
        
        <div style=""margin: 20px 0;"">
            {(content.ServiceLogoUrl != null ? $@"<img src=""{content.ServiceLogoUrl}"" alt=""{content.ServiceName}"" style=""max-height: 50px; margin-bottom: 10px;"">" : "")}
            <h3 style=""color: #2c3e50; margin: 0;"">{content.ServiceName}</h3>
        </div>
        
        <p style=""font-size: 16px; line-height: 1.6; color: #34495e;"">{content.Message}</p>
        
        {priceSection}
        {renewalSection}
        
        <div style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #ecf0f1;"">
            <a href=""{content.DashboardLink}"" style=""display: inline-block; background-color: #3498db; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; font-weight: bold;"">View Dashboard</a>
            {cancellationSection}
        </div>
        
        <div style=""margin-top: 40px; padding-top: 20px; border-top: 1px solid #ecf0f1; text-align: center; color: #95a5a6; font-size: 12px;"">
            <p>You received this email because you have alerts enabled for your WiseSub account.</p>
            <p><a href=""{_config.ApplicationBaseUrl}/settings"" style=""color: #3498db;"">Manage notification preferences</a></p>
        </div>
    </div>
</body>
</html>";
    }
    
    private string BuildDefaultAlertText(AlertEmailData emailData)
    {
        var content = emailData.Content;
        var lines = new List<string>
        {
            "WiseSub Alert",
            "=============",
            "",
            content.AlertType,
            "",
            $"Service: {content.ServiceName}",
            "",
            content.Message
        };
        
        if (content.CurrentPrice.HasValue)
        {
            var priceText = $"Amount: {FormatPrice(content.CurrentPrice, content.Currency)}";
            if (content.PreviousPrice.HasValue)
                priceText += $" (was {FormatPrice(content.PreviousPrice, content.Currency)})";
            lines.Add(priceText);
        }
        
        if (content.RenewalDate.HasValue)
            lines.Add($"Renewal Date: {content.RenewalDate:MMMM dd, yyyy}");
        
        lines.Add("");
        lines.Add($"View Dashboard: {content.DashboardLink}");
        
        if (!string.IsNullOrEmpty(content.CancellationLink))
            lines.Add($"Cancel Subscription: {content.CancellationLink}");
        
        lines.Add("");
        lines.Add("---");
        lines.Add("Manage notifications: " + _config.ApplicationBaseUrl + "/settings");
        
        return string.Join(Environment.NewLine, lines);
    }
    
    private string BuildDefaultDigestHtml(DailyDigestData digestData)
    {
        var alertRows = string.Join("", digestData.Alerts.Select(a => $@"
            <tr>
                <td style=""padding: 10px; border-bottom: 1px solid #ecf0f1;"">{a.AlertType}</td>
                <td style=""padding: 10px; border-bottom: 1px solid #ecf0f1;"">{a.ServiceName}</td>
                <td style=""padding: 10px; border-bottom: 1px solid #ecf0f1;"">{a.ActionDate:MMM dd}</td>
                <td style=""padding: 10px; border-bottom: 1px solid #ecf0f1;"">{FormatPrice(a.Amount, a.Currency)}</td>
            </tr>"));
        
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif; margin: 0; padding: 20px; background-color: #f5f6fa;"">
    <div style=""max-width: 600px; margin: 0 auto; background-color: white; border-radius: 8px; padding: 30px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);"">
        <div style=""text-align: center; margin-bottom: 30px;"">
            <h1 style=""color: #3498db; margin: 0;"">WiseSub</h1>
            <p style=""color: #7f8c8d;"">Daily Digest for {digestData.DigestDate:MMMM dd, yyyy}</p>
        </div>
        
        <div style=""background-color: #f8f9fa; border-radius: 8px; padding: 20px; margin-bottom: 30px;"">
            <h3 style=""margin-top: 0; color: #2c3e50;"">Summary</h3>
            <p>Hi {digestData.RecipientName},</p>
            <p>You have <strong>{digestData.Summary.TotalAlerts}</strong> alert(s) today:</p>
            <ul style=""color: #34495e;"">
                {(digestData.Summary.RenewalAlerts > 0 ? $"<li>{digestData.Summary.RenewalAlerts} renewal reminder(s)</li>" : "")}
                {(digestData.Summary.PriceChangeAlerts > 0 ? $"<li>{digestData.Summary.PriceChangeAlerts} price change(s)</li>" : "")}
                {(digestData.Summary.TrialEndingAlerts > 0 ? $"<li>{digestData.Summary.TrialEndingAlerts} trial ending</li>" : "")}
                {(digestData.Summary.UnusedSubscriptionAlerts > 0 ? $"<li>{digestData.Summary.UnusedSubscriptionAlerts} unused subscription(s)</li>" : "")}
            </ul>
            {(digestData.Summary.TotalUpcomingRenewals.HasValue ? $"<p><strong>Total upcoming renewals:</strong> {FormatPrice(digestData.Summary.TotalUpcomingRenewals, digestData.Summary.Currency)}</p>" : "")}
        </div>
        
        <table style=""width: 100%; border-collapse: collapse;"">
            <thead>
                <tr style=""background-color: #3498db; color: white;"">
                    <th style=""padding: 10px; text-align: left;"">Type</th>
                    <th style=""padding: 10px; text-align: left;"">Service</th>
                    <th style=""padding: 10px; text-align: left;"">Date</th>
                    <th style=""padding: 10px; text-align: left;"">Amount</th>
                </tr>
            </thead>
            <tbody>
                {alertRows}
            </tbody>
        </table>
        
        <div style=""margin-top: 30px; text-align: center;"">
            <a href=""{digestData.DashboardLink}"" style=""display: inline-block; background-color: #3498db; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; font-weight: bold;"">View Dashboard</a>
        </div>
        
        <div style=""margin-top: 40px; padding-top: 20px; border-top: 1px solid #ecf0f1; text-align: center; color: #95a5a6; font-size: 12px;"">
            <p>You received this daily digest because you have it enabled in your WiseSub settings.</p>
            <p><a href=""{_config.ApplicationBaseUrl}/settings"" style=""color: #3498db;"">Manage notification preferences</a></p>
        </div>
    </div>
</body>
</html>";
    }
    
    private string BuildDefaultDigestText(DailyDigestData digestData)
    {
        var lines = new List<string>
        {
            "WiseSub Daily Digest",
            "====================",
            $"Date: {digestData.DigestDate:MMMM dd, yyyy}",
            "",
            $"Hi {digestData.RecipientName},",
            "",
            $"You have {digestData.Summary.TotalAlerts} alert(s) today:",
            ""
        };
        
        if (digestData.Summary.RenewalAlerts > 0)
            lines.Add($"- {digestData.Summary.RenewalAlerts} renewal reminder(s)");
        if (digestData.Summary.PriceChangeAlerts > 0)
            lines.Add($"- {digestData.Summary.PriceChangeAlerts} price change(s)");
        if (digestData.Summary.TrialEndingAlerts > 0)
            lines.Add($"- {digestData.Summary.TrialEndingAlerts} trial ending");
        if (digestData.Summary.UnusedSubscriptionAlerts > 0)
            lines.Add($"- {digestData.Summary.UnusedSubscriptionAlerts} unused subscription(s)");
        
        if (digestData.Summary.TotalUpcomingRenewals.HasValue)
        {
            lines.Add("");
            lines.Add($"Total upcoming renewals: {FormatPrice(digestData.Summary.TotalUpcomingRenewals, digestData.Summary.Currency)}");
        }
        
        lines.Add("");
        lines.Add("Your Alerts:");
        lines.Add("------------");
        
        foreach (var alert in digestData.Alerts)
        {
            lines.Add($"- [{alert.AlertType}] {alert.ServiceName}");
            if (alert.ActionDate.HasValue)
                lines.Add($"  Date: {alert.ActionDate:MMM dd, yyyy}");
            if (alert.Amount.HasValue)
                lines.Add($"  Amount: {FormatPrice(alert.Amount, alert.Currency)}");
            lines.Add("");
        }
        
        lines.Add("---");
        lines.Add($"View Dashboard: {digestData.DashboardLink}");
        lines.Add($"Manage notifications: {_config.ApplicationBaseUrl}/settings");
        
        return string.Join(Environment.NewLine, lines);
    }
}

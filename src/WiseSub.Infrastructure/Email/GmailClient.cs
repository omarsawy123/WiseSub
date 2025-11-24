using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WiseSub.Application.Common.Extensions;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;
using WiseSub.Infrastructure.Security;
using System.Net.Http.Json;
using System.Text;

namespace WiseSub.Infrastructure.Email;

/// <summary>
/// Gmail API client implementation
/// </summary>
public class GmailClient : IGmailClient, IEmailProviderClient
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GmailClient> _logger;
    private readonly IEmailAccountRepository _emailAccountRepository;
    private readonly ITokenEncryptionService _tokenEncryptionService;
    private readonly HttpClient _httpClient;

    // Gmail API scopes required
    private static readonly string[] Scopes = { GmailService.Scope.GmailReadonly };

    public GmailClient(
        IConfiguration configuration,
        ILogger<GmailClient> logger,
        IEmailAccountRepository emailAccountRepository,
        ITokenEncryptionService tokenEncryptionService,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _emailAccountRepository = emailAccountRepository;
        _tokenEncryptionService = tokenEncryptionService;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<EmailConnectionResult> ConnectAccountAsync(
        string userId,
        string authorizationCode,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting Gmail account for user {UserId}", userId);

        // Exchange authorization code for tokens
        var tokenResponse = await ExchangeCodeForTokensAsync(authorizationCode, cancellationToken);
        if (tokenResponse == null)
        {
            return new EmailConnectionResult
            {
                Success = false,
                ErrorMessage = "Failed to exchange authorization code for tokens"
            };
        }

        // Get user's email address
        var emailAddress = await GetUserEmailAddressAsync(tokenResponse.AccessToken, cancellationToken);
        if (string.IsNullOrEmpty(emailAddress))
        {
            return new EmailConnectionResult
            {
                Success = false,
                ErrorMessage = "Failed to retrieve email address from Gmail"
            };
        }

        // Check if account already exists
        var existingAccount = await _emailAccountRepository.GetByEmailAddressAsync(emailAddress, cancellationToken);
        if (existingAccount != null)
        {
            // Update existing account
            var encryptedAccessToken = _tokenEncryptionService.Encrypt(tokenResponse.AccessToken);
            var encryptedRefreshToken = _tokenEncryptionService.Encrypt(tokenResponse.RefreshToken ?? "");
            var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            await _emailAccountRepository.UpdateTokensAsync(
                existingAccount.Id,
                encryptedAccessToken,
                encryptedRefreshToken,
                expiresAt,
                cancellationToken);

            return new EmailConnectionResult
            {
                Success = true,
                EmailAccountId = existingAccount.Id,
                EmailAddress = emailAddress,
                TokenExpiresAt = expiresAt
            };
        }

        // Create new email account
        var emailAccount = new EmailAccount
        {
            UserId = userId,
            EmailAddress = emailAddress,
            Provider = EmailProvider.Gmail,
            EncryptedAccessToken = _tokenEncryptionService.Encrypt(tokenResponse.AccessToken),
            EncryptedRefreshToken = _tokenEncryptionService.Encrypt(tokenResponse.RefreshToken ?? ""),
            TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
            ConnectedAt = DateTime.UtcNow,
            LastScanAt = DateTime.MinValue,
            IsActive = true
        };

        await _emailAccountRepository.AddAsync(emailAccount, cancellationToken);

        _logger.LogInformation("Successfully connected Gmail account {EmailAddress} for user {UserId}",
            emailAddress, userId);

        return new EmailConnectionResult
        {
            Success = true,
            EmailAccountId = emailAccount.Id,
            EmailAddress = emailAddress,
            TokenExpiresAt = emailAccount.TokenExpiresAt
        };
    }

    public async Task<IEnumerable<EmailMessage>> GetEmailsAsync(
        string emailAccountId,
        EmailFilter filter,
        CancellationToken cancellationToken = default)
    {
        // Get email account
        var emailAccount = await _emailAccountRepository.GetByIdAsync(emailAccountId, cancellationToken);
        if (emailAccount == null)
        {
            _logger.LogWarning("Email account {EmailAccountId} not found", emailAccountId);
            return Enumerable.Empty<EmailMessage>();
        }

        return await GetEmailsAsync(emailAccount, filter, cancellationToken);
    }

    public async Task<IEnumerable<EmailMessage>> GetEmailsAsync(
        EmailAccount emailAccount,
        EmailFilter filter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving emails for account {EmailAccountId}", emailAccount.Id);

        // Check if token needs refresh
        if (emailAccount.TokenExpiresAt <= DateTime.UtcNow.AddMinutes(5))
        {
            var refreshed = await RefreshAccessTokenAsync(emailAccount.Id, cancellationToken);
            if (!refreshed)
            {
                _logger.LogError("Failed to refresh access token for account {EmailAccountId}", emailAccount.Id);
                return Enumerable.Empty<EmailMessage>();
            }
            // Reload account with new token
            emailAccount = (await _emailAccountRepository.GetByIdAsync(emailAccount.Id, cancellationToken))!;
        }

        // Decrypt access token
        var accessToken = _tokenEncryptionService.Decrypt(emailAccount!.EncryptedAccessToken);

        // Create Gmail service
        var credential = GoogleCredential.FromAccessToken(accessToken);
        var service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "WiseSub"
        });

        // Build query string for Gmail API
        var query = BuildGmailQuery(filter);

        // List messages
        var listRequest = service.Users.Messages.List("me");
        listRequest.Q = query;
        listRequest.MaxResults = filter.MaxResults ?? 500;

        var messages = new List<EmailMessage>();
        var response = await listRequest.ExecuteAsync(cancellationToken);

        if (response.Messages == null || response.Messages.Count == 0)
        {
            _logger.LogInformation("No emails found for account {EmailAccountId} with filter", emailAccount.Id);
            return messages;
        }

        // Retrieve message details (in batches to respect rate limits)
        // Optimized: Increased batch size to 100 and using METADATA format for faster retrieval
        var batchSize = 100;
        for (int i = 0; i < response.Messages.Count; i += batchSize)
        {
            var batch = response.Messages.Skip(i).Take(batchSize);
            var batchTasks = batch.Select(async msg =>
            {
                var messageRequest = service.Users.Messages.Get("me", msg.Id);
                // Use METADATA format with specific headers for better performance
                messageRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;
                messageRequest.MetadataHeaders = new Google.Apis.Util.Repeatable<string>(new[] { "From", "Subject", "Date" });
                var message = await messageRequest.ExecuteAsync(cancellationToken);
                return ParseGmailMessage(message);
            });

            var batchResults = await Task.WhenAll(batchTasks);
            messages.AddRange(batchResults.Where(m => m != null)!);

            // Rate limiting: reduced delay for better performance
            if (i + batchSize < response.Messages.Count)
            {
                await Task.Delay(50, cancellationToken);
            }
        }

        // Store the current historyId for future incremental syncs
        if (response.Messages.Count > 0)
        {
            // Get the latest message to extract historyId
            var latestMessageRequest = service.Users.Messages.Get("me", response.Messages[0].Id);
            latestMessageRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Minimal;
            var latestMessage = await latestMessageRequest.ExecuteAsync(cancellationToken);
            
            if (latestMessage.HistoryId.HasValue)
            {
                await _emailAccountRepository.UpdateHistoryIdAsync(
                    emailAccount.Id, 
                    latestMessage.HistoryId.Value.ToString(), 
                    cancellationToken);
                
                _logger.LogInformation("Updated historyId to {HistoryId} for account {EmailAccountId}",
                    latestMessage.HistoryId.Value, emailAccount.Id);
            }
        }

        // Update last scan timestamp
        await _emailAccountRepository.UpdateLastScanAsync(emailAccount.Id, DateTime.UtcNow, cancellationToken);

        _logger.LogInformation("Retrieved {Count} emails for account {EmailAccountId}",
            messages.Count, emailAccount.Id);

        return messages;
    }

    public async Task<bool> RefreshAccessTokenAsync(
        string emailAccountId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing access token for account {EmailAccountId}", emailAccountId);

        // Get email account
        var emailAccount = await _emailAccountRepository.GetByIdAsync(emailAccountId, cancellationToken);
        if (emailAccount == null)
        {
            _logger.LogWarning("Email account {EmailAccountId} not found", emailAccountId);
            return false;
        }

        return await RefreshAccessTokenAsync(emailAccount, cancellationToken);
    }

    public async Task<bool> RefreshAccessTokenAsync(
        EmailAccount emailAccount,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing access token for account {EmailAccountId}", emailAccount.Id);

        // Decrypt refresh token
        var refreshToken = _tokenEncryptionService.Decrypt(emailAccount.EncryptedRefreshToken);
        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogError("No refresh token available for account {EmailAccountId}", emailAccount.Id);
            return false;
        }

        // Request new access token
        var clientId = _configuration["Authentication:Google:ClientId"];
        var clientSecret = _configuration["Authentication:Google:ClientSecret"];

        var requestData = new Dictionary<string, string>
        {
            { "client_id", clientId ?? "" },
            { "client_secret", clientSecret ?? "" },
            { "refresh_token", refreshToken },
            { "grant_type", "refresh_token" }
        };

        var response = await _httpClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(requestData),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to refresh token for account {EmailAccountId}: {Error}",
                emailAccount.Id, errorContent);
            return false;
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            _logger.LogError("Invalid token response for account {EmailAccountId}", emailAccount.Id);
            return false;
        }

        // Update tokens in database
        var encryptedAccessToken = _tokenEncryptionService.Encrypt(tokenResponse.AccessToken);
        var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

        await _emailAccountRepository.UpdateTokensAsync(
            emailAccount.Id,
            encryptedAccessToken,
            emailAccount.EncryptedRefreshToken, // Keep same refresh token
            expiresAt,
            cancellationToken);

        _logger.LogInformation("Successfully refreshed access token for account {EmailAccountId}", emailAccount.Id);
        return true;
    }

    public async Task<bool> RevokeAccessAsync(
        string emailAccountId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Revoking access for account {EmailAccountId}", emailAccountId);

        // Get email account
        var emailAccount = await _emailAccountRepository.GetByIdAsync(emailAccountId, cancellationToken);
        if (emailAccount == null)
        {
            _logger.LogWarning("Email account {EmailAccountId} not found", emailAccountId);
            return false;
        }

        return await RevokeAccessAsync(emailAccount, cancellationToken);
    }

    public async Task<bool> RevokeAccessAsync(
        EmailAccount emailAccount,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Revoking access for account {EmailAccountId}", emailAccount.Id);

        // Decrypt refresh token
        var refreshToken = _tokenEncryptionService.Decrypt(emailAccount.EncryptedRefreshToken);

        // Revoke token with Google
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var requestData = new Dictionary<string, string>
            {
                { "token", refreshToken }
            };

            var response = await _httpClient.PostAsync(
                "https://oauth2.googleapis.com/revoke",
                new FormUrlEncodedContent(requestData),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to revoke token with Google for account {EmailAccountId}",
                    emailAccount.Id);
            }
        }

        // Delete tokens and mark as inactive in database
        await _emailAccountRepository.RevokeAccessAsync(emailAccount.Id, cancellationToken);

        _logger.LogInformation("Successfully revoked access for account {EmailAccountId}", emailAccount.Id);
        return true;
    }

    private async Task<TokenResponse?> ExchangeCodeForTokensAsync(
        string authorizationCode,
        CancellationToken cancellationToken)
    {
        var clientId = _configuration["Authentication:Google:ClientId"];
        var clientSecret = _configuration["Authentication:Google:ClientSecret"];
        var redirectUri = _configuration["Authentication:Google:RedirectUri"];

        var requestData = new Dictionary<string, string>
        {
            { "code", authorizationCode },
            { "client_id", clientId ?? "" },
            { "client_secret", clientSecret ?? "" },
            { "redirect_uri", redirectUri ?? "" },
            { "grant_type", "authorization_code" },
            { "access_type", "offline" }, // Request refresh token
            { "scope", string.Join(" ", Scopes) }
        };

        var response = await _httpClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(requestData),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to exchange authorization code: {Error}", errorContent);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
    }

    private async Task<string?> GetUserEmailAddressAsync(string accessToken, CancellationToken cancellationToken)
    {
        var credential = GoogleCredential.FromAccessToken(accessToken);
        var service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "WiseSub"
        });

        var profile = await service.Users.GetProfile("me").ExecuteAsync(cancellationToken);
        return profile.EmailAddress;
    }

    private string BuildGmailQuery(EmailFilter filter)
    {
        var queryParts = new List<string>();

        // Date filters
        if (filter.Since.HasValue)
        {
            queryParts.Add($"after:{filter.Since.Value:yyyy/MM/dd}");
        }

        if (filter.Before.HasValue)
        {
            queryParts.Add($"before:{filter.Before.Value:yyyy/MM/dd}");
        }

        // Sender domain filters
        if (filter.SenderDomains.Any())
        {
            var senderQuery = string.Join(" OR ", filter.SenderDomains.Select(d => $"from:*@{d}"));
            queryParts.Add($"({senderQuery})");
        }

        // Subject keyword filters
        if (filter.SubjectKeywords.Any())
        {
            var subjectQuery = string.Join(" OR ", filter.SubjectKeywords.Select(k => $"subject:{k}"));
            queryParts.Add($"({subjectQuery})");
        }

        // Default subscription-related keywords if no specific filters
        if (!filter.SenderDomains.Any() && !filter.SubjectKeywords.Any())
        {
            var defaultKeywords = new[]
            {
                "subscription", "renewal", "invoice", "receipt", "payment",
                "billing", "charge", "trial", "upgrade", "membership"
            };
            var keywordQuery = string.Join(" OR ", defaultKeywords.Select(k => $"subject:{k}"));
            queryParts.Add($"({keywordQuery})");
        }

        return string.Join(" ", queryParts);
    }

    private EmailMessage ParseGmailMessage(Message message)
    {
        var emailMessage = new EmailMessage
        {
            Id = message.Id,
            ReceivedAt = DateTimeOffset.FromUnixTimeMilliseconds(message.InternalDate ?? 0).DateTime
        };

        // Parse headers
        if (message.Payload?.Headers != null)
        {
            foreach (var header in message.Payload.Headers)
            {
                switch (header.Name?.ToLower())
                {
                    case "from":
                        emailMessage.Sender = header.Value ?? "";
                        break;
                    case "subject":
                        emailMessage.Subject = header.Value ?? "";
                        break;
                }
            }
        }

        // Parse body
        emailMessage.Body = ExtractMessageBody(message.Payload);

        // Parse labels/folders
        if (message.LabelIds != null && message.LabelIds.Count > 0)
        {
            emailMessage.FolderId = message.LabelIds[0];
            emailMessage.FolderName = message.LabelIds[0];
        }

        return emailMessage;
    }

    private string ExtractMessageBody(MessagePart? part)
    {
        if (part == null)
            return string.Empty;

        // If this part has body data, decode it
        if (!string.IsNullOrEmpty(part.Body?.Data))
        {
            var data = part.Body.Data.Replace('-', '+').Replace('_', '/');
            var bytes = Convert.FromBase64String(data);
            return Encoding.UTF8.GetString(bytes);
        }

        // If this part has sub-parts, recursively extract
        if (part.Parts != null && part.Parts.Count > 0)
        {
            // Prefer HTML parts, fall back to plain text
            var htmlPart = part.Parts.FirstOrDefault(p => p.MimeType == "text/html");
            if (htmlPart != null)
            {
                return ExtractMessageBody(htmlPart);
            }

            var textPart = part.Parts.FirstOrDefault(p => p.MimeType == "text/plain");
            if (textPart != null)
            {
                return ExtractMessageBody(textPart);
            }

            // Try first part
            return ExtractMessageBody(part.Parts[0]);
        }

        return string.Empty;
    }

    public async Task<IEnumerable<EmailMessage>> GetNewEmailsSinceLastScanAsync(
        string emailAccountId,
        EmailFilter filter,
        CancellationToken cancellationToken = default)
    {
        // Get email account
        var emailAccount = await _emailAccountRepository.GetByIdAsync(emailAccountId, cancellationToken);
        if (emailAccount == null)
        {
            _logger.LogWarning("Email account {EmailAccountId} not found", emailAccountId);
            return Enumerable.Empty<EmailMessage>();
        }

        return await GetNewEmailsSinceLastScanAsync(emailAccount, filter, cancellationToken);
    }

    public async Task<IEnumerable<EmailMessage>> GetNewEmailsSinceLastScanAsync(
        EmailAccount emailAccount,
        EmailFilter filter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving new emails since last scan for account {EmailAccountId}", emailAccount.Id);

        // If no sync token exists, fall back to full scan
        var syncToken = emailAccount.GetProviderSyncToken();
        if (string.IsNullOrEmpty(syncToken))
        {
            _logger.LogInformation("No sync token found for account {EmailAccountId}, performing full scan", emailAccount.Id);
            return await GetEmailsAsync(emailAccount, filter, cancellationToken);
        }

        // Check if token needs refresh
        if (emailAccount.TokenExpiresAt <= DateTime.UtcNow.AddMinutes(5))
        {
            var refreshed = await RefreshAccessTokenAsync(emailAccount.Id, cancellationToken);
            if (!refreshed)
            {
                _logger.LogError("Failed to refresh access token for account {EmailAccountId}", emailAccount.Id);
                return Enumerable.Empty<EmailMessage>();
            }
            // Reload account with new token
            emailAccount = (await _emailAccountRepository.GetByIdAsync(emailAccount.Id, cancellationToken))!;
        }

        // Decrypt access token
        var accessToken = _tokenEncryptionService.Decrypt(emailAccount!.EncryptedAccessToken);

        // Create Gmail service
        var credential = GoogleCredential.FromAccessToken(accessToken);
        var service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "WiseSub"
        });

        // Use Gmail History API to get changes since last historyId
        var historyRequest = service.Users.History.List("me");
        historyRequest.StartHistoryId = ulong.Parse(syncToken!);
        historyRequest.HistoryTypes = UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded;

        var messages = new List<EmailMessage>();
        var newMessageIds = new HashSet<string>();

        try
        {
            var historyResponse = await historyRequest.ExecuteAsync(cancellationToken);

            if (historyResponse.History != null && historyResponse.History.Count > 0)
            {
                // Collect all new message IDs from history
                foreach (var history in historyResponse.History)
                {
                    if (history.MessagesAdded != null)
                    {
                        foreach (var messageAdded in history.MessagesAdded)
                        {
                            if (messageAdded.Message?.Id != null)
                            {
                                newMessageIds.Add(messageAdded.Message.Id);
                            }
                        }
                    }
                }

                _logger.LogInformation("Found {Count} new messages from history for account {EmailAccountId}",
                    newMessageIds.Count, emailAccount.Id);

                // Retrieve message details for new messages (in batches)
                // Optimized: Increased batch size to 100 and using METADATA format
                var batchSize = 100;
                var messageIdList = newMessageIds.ToList();
                
                for (int i = 0; i < messageIdList.Count; i += batchSize)
                {
                    var batch = messageIdList.Skip(i).Take(batchSize);
                    var batchTasks = batch.Select(async msgId =>
                    {
                        var messageRequest = service.Users.Messages.Get("me", msgId);
                        // Use METADATA format with specific headers for better performance
                        messageRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;
                        messageRequest.MetadataHeaders = new Google.Apis.Util.Repeatable<string>(new[] { "From", "Subject", "Date" });
                        var message = await messageRequest.ExecuteAsync(cancellationToken);
                        
                        // Apply filter criteria
                        var parsedMessage = ParseGmailMessage(message);
                        if (MatchesFilter(parsedMessage, filter))
                        {
                            return parsedMessage;
                        }
                        return null;
                    });

                    var batchResults = await Task.WhenAll(batchTasks);
                    messages.AddRange(batchResults.Where(m => m != null)!);

                    // Rate limiting: reduced delay for better performance
                    if (i + batchSize < messageIdList.Count)
                    {
                        await Task.Delay(50, cancellationToken);
                    }
                }

                // Update historyId to the latest
                if (historyResponse.HistoryId.HasValue)
                {
                    await _emailAccountRepository.UpdateHistoryIdAsync(
                        emailAccount.Id,
                        historyResponse.HistoryId.Value.ToString(),
                        cancellationToken);
                    
                    _logger.LogInformation("Updated historyId to {HistoryId} for account {EmailAccountId}",
                        historyResponse.HistoryId.Value, emailAccount.Id);
                }
            }
            else
            {
                _logger.LogInformation("No new messages found in history for account {EmailAccountId}", emailAccount.Id);
            }
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // HistoryId is too old or invalid, fall back to full scan
            _logger.LogWarning("Sync token {SyncToken} is invalid or too old for account {EmailAccountId}, performing full scan",
                syncToken, emailAccount.Id);
            
            // Clear the invalid sync token
            emailAccount.SetProviderSyncToken(null);
            await _emailAccountRepository.UpdateHistoryIdAsync(emailAccount.Id, null!, cancellationToken);
            
            // Perform full scan
            return await GetEmailsAsync(emailAccount.Id, filter, cancellationToken);
        }

        // Update last scan timestamp
        await _emailAccountRepository.UpdateLastScanAsync(emailAccount.Id, DateTime.UtcNow, cancellationToken);

        _logger.LogInformation("Retrieved {Count} new emails for account {EmailAccountId} using incremental sync",
            messages.Count, emailAccount.Id);

        return messages;
    }

    private bool MatchesFilter(EmailMessage message, EmailFilter filter)
    {
        // Check date range
        if (filter.Since.HasValue && message.ReceivedAt < filter.Since.Value)
            return false;

        if (filter.Before.HasValue && message.ReceivedAt > filter.Before.Value)
            return false;

        // Check sender domains
        if (filter.SenderDomains.Any())
        {
            var senderDomain = message.Sender.Split('@').LastOrDefault()?.ToLower();
            if (senderDomain == null || !filter.SenderDomains.Any(d => senderDomain.Contains(d.ToLower())))
                return false;
        }

        // Check subject keywords
        if (filter.SubjectKeywords.Any())
        {
            var subjectLower = message.Subject.ToLower();
            if (!filter.SubjectKeywords.Any(k => subjectLower.Contains(k.ToLower())))
                return false;
        }

        // If no specific filters, check default subscription keywords
        if (!filter.SenderDomains.Any() && !filter.SubjectKeywords.Any())
        {
            var defaultKeywords = new[]
            {
                "subscription", "renewal", "invoice", "receipt", "payment",
                "billing", "charge", "trial", "upgrade", "membership"
            };
            var subjectLower = message.Subject.ToLower();
            if (!defaultKeywords.Any(k => subjectLower.Contains(k)))
                return false;
        }

        return true;
    }

    public bool SupportsProvider(EmailProvider provider)
    {
        return provider == EmailProvider.Gmail;
    }

    private class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = string.Empty;
    }
}

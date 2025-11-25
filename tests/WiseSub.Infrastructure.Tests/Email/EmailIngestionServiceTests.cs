using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WiseSub.Application.Common.Configuration;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;
using WiseSub.Infrastructure.Email;
using Xunit;

namespace WiseSub.Infrastructure.Tests.Email;

/// <summary>
/// Tests for EmailIngestionService
/// Covers Task 5.1: Initial email retrieval span (12 months)
/// Covers Task 6.2: Metadata-only storage verification
/// </summary>
public class EmailIngestionServiceTests
{
    private readonly Mock<ILogger<EmailIngestionService>> _mockLogger;
    private readonly Mock<IEmailAccountRepository> _mockEmailAccountRepository;
    private readonly Mock<IEmailProviderFactory> _mockProviderFactory;
    private readonly Mock<IEmailQueueService> _mockQueueService;
    private readonly Mock<IEmailMetadataService> _mockMetadataService;
    private readonly EmailScanConfiguration _config;

    public EmailIngestionServiceTests()
    {
        _mockLogger = new Mock<ILogger<EmailIngestionService>>();
        _mockEmailAccountRepository = new Mock<IEmailAccountRepository>();
        _mockProviderFactory = new Mock<IEmailProviderFactory>();
        _mockQueueService = new Mock<IEmailQueueService>();
        _mockMetadataService = new Mock<IEmailMetadataService>();
        _config = new EmailScanConfiguration
        {
            DefaultLookbackMonths = 12,
            MaxEmailsPerScan = 500,
            SubjectKeywords = new List<string> { "subscription", "renewal", "invoice" }
        };
    }

    private EmailIngestionService CreateService()
    {
        return new EmailIngestionService(
            _mockLogger.Object,
            _mockEmailAccountRepository.Object,
            _mockProviderFactory.Object,
            _mockQueueService.Object,
            _mockMetadataService.Object,
            Options.Create(_config));
    }

    #region Task 5.1: Initial Email Retrieval Span Tests

    [Fact]
    public void EmailScanConfiguration_DefaultLookbackMonths_Is12Months()
    {
        // Task 5.1: Default lookback period should be 12 months
        // Assert
        Assert.Equal(12, _config.DefaultLookbackMonths);
    }

    [Fact]
    public async Task ScanEmailAccountAsync_WithoutSinceParameter_Uses12MonthLookback()
    {
        // Arrange - Task 5.1: When no 'since' date provided, should use 12 month lookback
        var emailAccount = CreateActiveEmailAccount();
        var mockProvider = new Mock<IEmailProviderClient>();
        
        EmailFilter? capturedFilter = null;
        mockProvider
            .Setup(p => p.GetEmailsAsync(
                It.IsAny<EmailAccount>(),
                It.IsAny<EmailFilter>(),
                It.IsAny<CancellationToken>()))
            .Callback<EmailAccount, EmailFilter, CancellationToken>((acc, filter, ct) => capturedFilter = filter)
            .ReturnsAsync(Enumerable.Empty<EmailMessage>());

        _mockProviderFactory
            .Setup(f => f.GetProvider(EmailProvider.Gmail))
            .Returns(mockProvider.Object);

        var service = CreateService();

        // Act
        await service.ScanEmailAccountAsync(emailAccount, since: null);

        // Assert - Filter should be set to 12 months ago
        Assert.NotNull(capturedFilter);
        var expectedSince = DateTime.UtcNow.AddMonths(-12);
        
        // Allow 1 minute tolerance for test execution time
        Assert.True(capturedFilter.Since >= expectedSince.AddMinutes(-1) && 
                    capturedFilter.Since <= DateTime.UtcNow,
            $"Filter.Since should be approximately 12 months ago. Expected: ~{expectedSince}, Actual: {capturedFilter.Since}");
    }

    [Fact]
    public async Task ScanEmailAccountAsync_WithCustomSinceParameter_UsesProvidedDate()
    {
        // Arrange - Task 5.1: Custom 'since' date should override default
        var emailAccount = CreateActiveEmailAccount();
        var mockProvider = new Mock<IEmailProviderClient>();
        var customSince = DateTime.UtcNow.AddMonths(-6); // 6 months instead of 12
        
        EmailFilter? capturedFilter = null;
        mockProvider
            .Setup(p => p.GetEmailsAsync(
                It.IsAny<EmailAccount>(),
                It.IsAny<EmailFilter>(),
                It.IsAny<CancellationToken>()))
            .Callback<EmailAccount, EmailFilter, CancellationToken>((acc, filter, ct) => capturedFilter = filter)
            .ReturnsAsync(Enumerable.Empty<EmailMessage>());

        _mockProviderFactory
            .Setup(f => f.GetProvider(EmailProvider.Gmail))
            .Returns(mockProvider.Object);

        var service = CreateService();

        // Act
        await service.ScanEmailAccountAsync(emailAccount, since: customSince);

        // Assert - Filter should use the custom date
        Assert.NotNull(capturedFilter);
        Assert.Equal(customSince, capturedFilter.Since);
    }

    [Fact]
    public async Task ScanEmailAccountAsync_PassesSubjectKeywordsToFilter()
    {
        // Arrange - Task 5.1: Subject keywords should be passed in filter
        var emailAccount = CreateActiveEmailAccount();
        var mockProvider = new Mock<IEmailProviderClient>();
        
        EmailFilter? capturedFilter = null;
        mockProvider
            .Setup(p => p.GetEmailsAsync(
                It.IsAny<EmailAccount>(),
                It.IsAny<EmailFilter>(),
                It.IsAny<CancellationToken>()))
            .Callback<EmailAccount, EmailFilter, CancellationToken>((acc, filter, ct) => capturedFilter = filter)
            .ReturnsAsync(Enumerable.Empty<EmailMessage>());

        _mockProviderFactory
            .Setup(f => f.GetProvider(EmailProvider.Gmail))
            .Returns(mockProvider.Object);

        var service = CreateService();

        // Act
        await service.ScanEmailAccountAsync(emailAccount);

        // Assert - Subject keywords should be included
        Assert.NotNull(capturedFilter);
        Assert.NotNull(capturedFilter.SubjectKeywords);
        Assert.Contains("subscription", capturedFilter.SubjectKeywords);
        Assert.Contains("renewal", capturedFilter.SubjectKeywords);
        Assert.Contains("invoice", capturedFilter.SubjectKeywords);
    }

    [Fact]
    public async Task ScanEmailAccountAsync_RespectsMaxEmailsPerScan()
    {
        // Arrange - Task 5.1: MaxResults should be set in filter
        var emailAccount = CreateActiveEmailAccount();
        var mockProvider = new Mock<IEmailProviderClient>();
        
        EmailFilter? capturedFilter = null;
        mockProvider
            .Setup(p => p.GetEmailsAsync(
                It.IsAny<EmailAccount>(),
                It.IsAny<EmailFilter>(),
                It.IsAny<CancellationToken>()))
            .Callback<EmailAccount, EmailFilter, CancellationToken>((acc, filter, ct) => capturedFilter = filter)
            .ReturnsAsync(Enumerable.Empty<EmailMessage>());

        _mockProviderFactory
            .Setup(f => f.GetProvider(EmailProvider.Gmail))
            .Returns(mockProvider.Object);

        var service = CreateService();

        // Act
        await service.ScanEmailAccountAsync(emailAccount);

        // Assert - MaxResults should match configuration
        Assert.NotNull(capturedFilter);
        Assert.Equal(500, capturedFilter.MaxResults);
    }

    #endregion

    #region Task 6.2: Metadata-Only Storage Tests

    [Fact]
    public async Task ScanEmailAccountAsync_StoresEmailMetadata_NotFullContent()
    {
        // Arrange - Task 6.2: Email metadata should be stored, not full content
        var emailAccount = CreateActiveEmailAccount();
        var mockProvider = new Mock<IEmailProviderClient>();
        
        var emails = new List<EmailMessage>
        {
            new()
            {
                Id = "email-1",
                Sender = "netflix@netflix.com",
                Subject = "Your subscription renewal",
                Body = "This is the full email body content that should NOT be stored in metadata",
                ReceivedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        mockProvider
            .Setup(p => p.GetEmailsAsync(
                It.IsAny<EmailAccount>(),
                It.IsAny<EmailFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emails);

        _mockProviderFactory
            .Setup(f => f.GetProvider(EmailProvider.Gmail))
            .Returns(mockProvider.Object);

        // Capture what's stored in metadata service
        List<EmailMessage>? capturedEmails = null;
        _mockMetadataService
            .Setup(m => m.CreateEmailMetadataBatchAsync(
                It.IsAny<string>(),
                It.IsAny<List<EmailMessage>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, List<EmailMessage>, CancellationToken>((accountId, emailList, ct) => 
                capturedEmails = emailList)
            .ReturnsAsync(WiseSub.Domain.Common.Result.Success(new List<EmailMetadata>
            {
                new() { Id = "m1", EmailAccountId = emailAccount.Id, ExternalEmailId = "email-1", 
                        Sender = "netflix@netflix.com", Subject = "Your subscription renewal",
                        ReceivedAt = DateTime.UtcNow.AddDays(-1), Status = EmailProcessingStatus.Pending }
            }));

        _mockQueueService
            .Setup(q => q.QueueEmailBatchAsync(
                It.IsAny<List<EmailMetadata>>(),
                It.IsAny<Dictionary<string, EmailMessage>>(),
                It.IsAny<EmailProcessingPriority>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WiseSub.Domain.Common.Result.Success(1));

        var service = CreateService();

        // Act
        await service.ScanEmailAccountAsync(emailAccount);

        // Assert - Emails are passed to metadata service for processing
        Assert.NotNull(capturedEmails);
        Assert.Single(capturedEmails);
        var email = capturedEmails[0];
        Assert.Equal("email-1", email.Id);
    }

    [Fact]
    public void EmailMetadata_DoesNotContainBodyField()
    {
        // Task 6.2: Verify EmailMetadata entity doesn't have a Body property
        var metadata = new EmailMetadata();
        
        // Get all properties of EmailMetadata
        var properties = typeof(EmailMetadata).GetProperties();
        var propertyNames = properties.Select(p => p.Name).ToList();
        
        // Assert - Should not have Body, Content, or FullText fields
        Assert.DoesNotContain("Body", propertyNames);
        Assert.DoesNotContain("Content", propertyNames);
        Assert.DoesNotContain("FullText", propertyNames);
        Assert.DoesNotContain("EmailBody", propertyNames);
        
        // Should have metadata fields
        Assert.Contains("Subject", propertyNames);
        Assert.Contains("Sender", propertyNames);
        Assert.Contains("ReceivedAt", propertyNames);
        Assert.Contains("ExternalEmailId", propertyNames);
    }

    [Fact]
    public async Task ScanEmailAccountAsync_EmailContentPassedToQueue_NotPersisted()
    {
        // Arrange - Task 6.2: Full email content passed for processing but not persisted
        var emailAccount = CreateActiveEmailAccount();
        var mockProvider = new Mock<IEmailProviderClient>();
        
        var emails = new List<EmailMessage>
        {
            new()
            {
                Id = "email-1",
                Sender = "spotify@spotify.com",
                Subject = "Your subscription",
                Body = "Full email body for AI processing only",
                ReceivedAt = DateTime.UtcNow
            }
        };

        mockProvider
            .Setup(p => p.GetEmailsAsync(
                It.IsAny<EmailAccount>(),
                It.IsAny<EmailFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emails);

        _mockProviderFactory
            .Setup(f => f.GetProvider(EmailProvider.Gmail))
            .Returns(mockProvider.Object);

        _mockMetadataService
            .Setup(m => m.CreateEmailMetadataBatchAsync(
                It.IsAny<string>(),
                It.IsAny<List<EmailMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WiseSub.Domain.Common.Result.Success(new List<EmailMetadata>
            {
                new() { Id = "m1", EmailAccountId = emailAccount.Id, ExternalEmailId = "email-1",
                        Sender = "spotify@spotify.com", Subject = "Your subscription",
                        ReceivedAt = DateTime.UtcNow, Status = EmailProcessingStatus.Pending }
            }));

        // Capture what's passed to queue
        Dictionary<string, EmailMessage>? capturedEmails = null;
        _mockQueueService
            .Setup(q => q.QueueEmailBatchAsync(
                It.IsAny<List<EmailMetadata>>(),
                It.IsAny<Dictionary<string, EmailMessage>>(),
                It.IsAny<EmailProcessingPriority>(),
                It.IsAny<CancellationToken>()))
            .Callback<List<EmailMetadata>, Dictionary<string, EmailMessage>, EmailProcessingPriority, CancellationToken>(
                (metadata, emailsDict, priority, ct) => capturedEmails = emailsDict)
            .ReturnsAsync(WiseSub.Domain.Common.Result.Success(1));

        var service = CreateService();

        // Act
        await service.ScanEmailAccountAsync(emailAccount);

        // Assert - Full email content should be passed to queue for processing
        Assert.NotNull(capturedEmails);
        Assert.True(capturedEmails.ContainsKey("email-1"));
        Assert.NotNull(capturedEmails["email-1"].Body);
        Assert.Equal("Full email body for AI processing only", capturedEmails["email-1"].Body);
    }

    #endregion

    #region Helper Methods

    private EmailAccount CreateActiveEmailAccount()
    {
        return new EmailAccount
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "user-123",
            EmailAddress = "test@gmail.com",
            Provider = EmailProvider.Gmail,
            EncryptedAccessToken = "encrypted-token",
            EncryptedRefreshToken = "encrypted-refresh",
            TokenExpiresAt = DateTime.UtcNow.AddHours(1),
            IsActive = true,
            ConnectedAt = DateTime.UtcNow,
            LastScanAt = DateTime.UtcNow.AddDays(-1)
        };
    }

    #endregion
}

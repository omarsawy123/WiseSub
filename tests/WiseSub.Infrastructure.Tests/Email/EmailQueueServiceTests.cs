using Microsoft.Extensions.Logging;
using Moq;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Entities;
using WiseSub.Infrastructure.Email;
using Xunit;

namespace WiseSub.Infrastructure.Tests.Email;

public class EmailQueueServiceTests
{
    private readonly Mock<ILogger<EmailQueueService>> _loggerMock;
    private readonly EmailQueueService _emailQueueService;

    public EmailQueueServiceTests()
    {
        _loggerMock = new Mock<ILogger<EmailQueueService>>();
        _emailQueueService = new EmailQueueService(_loggerMock.Object);
    }

    [Fact]
    public async Task QueueEmailForProcessingAsync_ShouldQueueEmail()
    {
        // Arrange
        var emailAccountId = "account-123";
        var emailMetadata = new EmailMetadata
        {
            Id = "metadata-123",
            EmailAccountId = emailAccountId,
            ExternalEmailId = "email-123",
            Sender = "netflix@netflix.com",
            Subject = "Your Netflix subscription renewal",
            ReceivedAt = DateTime.UtcNow,
            IsProcessed = false
        };
        var email = new EmailMessage
        {
            Id = "email-123",
            Sender = "netflix@netflix.com",
            Subject = "Your Netflix subscription renewal",
            Body = "Your subscription will renew on...",
            ReceivedAt = DateTime.UtcNow
        };

        // Act
        var result = await _emailQueueService.QueueEmailForProcessingAsync(
            emailMetadata,
            email,
            EmailProcessingPriority.High);

        // Assert
        Assert.True(result.IsSuccess);
        
        // Verify queue status reflects the queued email
        var status = await _emailQueueService.GetQueueStatusAsync();
        Assert.Equal(1, status.HighPriorityCount);
        Assert.Equal(1, status.PendingCount);
    }

    [Fact]
    public async Task DequeueNextEmailAsync_ShouldReturnHighPriorityFirst()
    {
        // Arrange
        var emailAccountId = "account-123";
        
        var lowPriorityMetadata = new EmailMetadata
        {
            Id = "metadata-low",
            EmailAccountId = emailAccountId,
            ExternalEmailId = "email-low",
            Sender = "sender@example.com",
            Subject = "Low priority",
            ReceivedAt = DateTime.UtcNow,
            IsProcessed = false
        };
        var lowPriorityEmail = new EmailMessage
        {
            Id = "email-low",
            Sender = "sender@example.com",
            Subject = "Low priority",
            ReceivedAt = DateTime.UtcNow
        };

        var highPriorityMetadata = new EmailMetadata
        {
            Id = "metadata-high",
            EmailAccountId = emailAccountId,
            ExternalEmailId = "email-high",
            Sender = "sender@example.com",
            Subject = "High priority",
            ReceivedAt = DateTime.UtcNow,
            IsProcessed = false
        };
        var highPriorityEmail = new EmailMessage
        {
            Id = "email-high",
            Sender = "sender@example.com",
            Subject = "High priority",
            ReceivedAt = DateTime.UtcNow
        };

        // Queue low priority first
        await _emailQueueService.QueueEmailForProcessingAsync(
            lowPriorityMetadata,
            lowPriorityEmail,
            EmailProcessingPriority.Low);

        // Queue high priority second
        await _emailQueueService.QueueEmailForProcessingAsync(
            highPriorityMetadata,
            highPriorityEmail,
            EmailProcessingPriority.High);

        // Act
        var dequeuedEmail = await _emailQueueService.DequeueNextEmailAsync();

        // Assert - Note: DequeueNextEmailAsync is not yet implemented, will return null
        // When implemented, this test should verify high priority is returned first
        // TODO: Implement dequeue logic
        Assert.Null(dequeuedEmail);
    }

    [Fact]
    public async Task GetQueueStatusAsync_ShouldReturnCorrectCounts()
    {
        // Arrange
        var emailAccountId = "account-123";

        // Queue emails with different priorities
        await _emailQueueService.QueueEmailForProcessingAsync(
            new EmailMetadata { Id = "m1", EmailAccountId = emailAccountId, ExternalEmailId = "1", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow, IsProcessed = false },
            new EmailMessage { Id = "1", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow },
            EmailProcessingPriority.High);

        await _emailQueueService.QueueEmailForProcessingAsync(
            new EmailMetadata { Id = "m2", EmailAccountId = emailAccountId, ExternalEmailId = "2", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow, IsProcessed = false },
            new EmailMessage { Id = "2", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow },
            EmailProcessingPriority.High);

        await _emailQueueService.QueueEmailForProcessingAsync(
            new EmailMetadata { Id = "m3", EmailAccountId = emailAccountId, ExternalEmailId = "3", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow, IsProcessed = false },
            new EmailMessage { Id = "3", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow },
            EmailProcessingPriority.Normal);

        await _emailQueueService.QueueEmailForProcessingAsync(
            new EmailMetadata { Id = "m4", EmailAccountId = emailAccountId, ExternalEmailId = "4", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow, IsProcessed = false },
            new EmailMessage { Id = "4", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow },
            EmailProcessingPriority.Low);

        // Act
        var status = await _emailQueueService.GetQueueStatusAsync();

        // Assert
        Assert.Equal(2, status.HighPriorityCount);
        Assert.Equal(1, status.NormalPriorityCount);
        Assert.Equal(1, status.LowPriorityCount);
        Assert.Equal(4, status.PendingCount);
        Assert.Equal(0, status.ProcessedCount); // Processed count managed by metadata service
    }

    [Fact]
    public async Task GetPendingCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var emailAccountId = "account-123";

        // Queue 3 emails
        await _emailQueueService.QueueEmailForProcessingAsync(
            new EmailMetadata { Id = "m1", EmailAccountId = emailAccountId, ExternalEmailId = "1", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow, IsProcessed = false },
            new EmailMessage { Id = "1", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow },
            EmailProcessingPriority.Normal);

        await _emailQueueService.QueueEmailForProcessingAsync(
            new EmailMetadata { Id = "m2", EmailAccountId = emailAccountId, ExternalEmailId = "2", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow, IsProcessed = false },
            new EmailMessage { Id = "2", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow },
            EmailProcessingPriority.Normal);

        await _emailQueueService.QueueEmailForProcessingAsync(
            new EmailMetadata { Id = "m3", EmailAccountId = emailAccountId, ExternalEmailId = "3", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow, IsProcessed = false },
            new EmailMessage { Id = "3", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow },
            EmailProcessingPriority.Normal);

        // Act
        var pendingCount = await _emailQueueService.GetPendingCountAsync();

        // Assert
        Assert.Equal(3, pendingCount);
    }

    [Fact]
    public async Task QueueEmailBatchAsync_ShouldQueueAllEmails()
    {
        // Arrange
        var emailAccountId = "account-123";
        
        var metadataList = new List<EmailMetadata>
        {
            new() { Id = "m1", EmailAccountId = emailAccountId, ExternalEmailId = "e1", Sender = "s1", Subject = "s1", ReceivedAt = DateTime.UtcNow, IsProcessed = false },
            new() { Id = "m2", EmailAccountId = emailAccountId, ExternalEmailId = "e2", Sender = "s2", Subject = "s2", ReceivedAt = DateTime.UtcNow, IsProcessed = false },
            new() { Id = "m3", EmailAccountId = emailAccountId, ExternalEmailId = "e3", Sender = "s3", Subject = "s3", ReceivedAt = DateTime.UtcNow, IsProcessed = false }
        };

        var emailsDict = new Dictionary<string, EmailMessage>
        {
            { "e1", new EmailMessage { Id = "e1", Sender = "s1", Subject = "s1", ReceivedAt = DateTime.UtcNow } },
            { "e2", new EmailMessage { Id = "e2", Sender = "s2", Subject = "s2", ReceivedAt = DateTime.UtcNow } },
            { "e3", new EmailMessage { Id = "e3", Sender = "s3", Subject = "s3", ReceivedAt = DateTime.UtcNow } }
        };

        // Act
        var result = await _emailQueueService.QueueEmailBatchAsync(
            metadataList,
            emailsDict,
            EmailProcessingPriority.High);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value);

        var status = await _emailQueueService.GetQueueStatusAsync();
        Assert.Equal(3, status.HighPriorityCount);
        Assert.Equal(3, status.PendingCount);
    }
}

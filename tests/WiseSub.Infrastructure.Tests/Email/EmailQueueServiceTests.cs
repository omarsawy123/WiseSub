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
    private readonly Mock<IEmailMetadataRepository> _emailMetadataRepositoryMock;
    private readonly EmailQueueService _emailQueueService;

    public EmailQueueServiceTests()
    {
        _loggerMock = new Mock<ILogger<EmailQueueService>>();
        _emailMetadataRepositoryMock = new Mock<IEmailMetadataRepository>();
        _emailQueueService = new EmailQueueService(
            _loggerMock.Object,
            _emailMetadataRepositoryMock.Object);
    }

    [Fact]
    public async Task QueueEmailForProcessingAsync_ShouldCreateEmailMetadata_AndQueueEmail()
    {
        // Arrange
        var emailAccountId = "account-123";
        var email = new EmailMessage
        {
            Id = "email-123",
            Sender = "netflix@netflix.com",
            Subject = "Your Netflix subscription renewal",
            Body = "Your subscription will renew on...",
            ReceivedAt = DateTime.UtcNow
        };

        _emailMetadataRepositoryMock
            .Setup(x => x.GetByExternalEmailIdAsync(email.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailMetadata?)null);

        _emailMetadataRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<EmailMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailMetadata em, CancellationToken ct) => em);

        // Act
        var metadataId = await _emailQueueService.QueueEmailForProcessingAsync(
            emailAccountId,
            email,
            EmailProcessingPriority.High);

        // Assert
        Assert.NotNull(metadataId);
        Assert.NotEmpty(metadataId);

        _emailMetadataRepositoryMock.Verify(
            x => x.AddAsync(It.Is<EmailMetadata>(em =>
                em.EmailAccountId == emailAccountId &&
                em.ExternalEmailId == email.Id &&
                em.Sender == email.Sender &&
                em.Subject == email.Subject &&
                em.IsProcessed == false
            ), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueueEmailForProcessingAsync_ShouldSkipDuplicateEmail()
    {
        // Arrange
        var emailAccountId = "account-123";
        var email = new EmailMessage
        {
            Id = "email-123",
            Sender = "netflix@netflix.com",
            Subject = "Your Netflix subscription renewal",
            Body = "Your subscription will renew on...",
            ReceivedAt = DateTime.UtcNow
        };

        var existingMetadata = new EmailMetadata
        {
            Id = "metadata-123",
            EmailAccountId = emailAccountId,
            ExternalEmailId = email.Id,
            Sender = email.Sender,
            Subject = email.Subject,
            ReceivedAt = email.ReceivedAt,
            IsProcessed = false
        };

        _emailMetadataRepositoryMock
            .Setup(x => x.GetByExternalEmailIdAsync(email.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMetadata);

        // Act
        var metadataId = await _emailQueueService.QueueEmailForProcessingAsync(
            emailAccountId,
            email,
            EmailProcessingPriority.High);

        // Assert
        Assert.Equal(existingMetadata.Id, metadataId);

        _emailMetadataRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<EmailMetadata>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DequeueNextEmailAsync_ShouldReturnHighPriorityFirst()
    {
        // Arrange
        var emailAccountId = "account-123";
        
        var lowPriorityEmail = new EmailMessage
        {
            Id = "email-low",
            Sender = "sender@example.com",
            Subject = "Low priority",
            ReceivedAt = DateTime.UtcNow
        };

        var highPriorityEmail = new EmailMessage
        {
            Id = "email-high",
            Sender = "sender@example.com",
            Subject = "High priority",
            ReceivedAt = DateTime.UtcNow
        };

        _emailMetadataRepositoryMock
            .Setup(x => x.GetByExternalEmailIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailMetadata?)null);

        _emailMetadataRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<EmailMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailMetadata em, CancellationToken ct) => em);

        // Queue low priority first
        await _emailQueueService.QueueEmailForProcessingAsync(
            emailAccountId,
            lowPriorityEmail,
            EmailProcessingPriority.Low);

        // Queue high priority second
        await _emailQueueService.QueueEmailForProcessingAsync(
            emailAccountId,
            highPriorityEmail,
            EmailProcessingPriority.High);

        // Act
        var dequeuedEmail = await _emailQueueService.DequeueNextEmailAsync();

        // Assert
        Assert.NotNull(dequeuedEmail);
        Assert.Equal("email-high", dequeuedEmail.Email.Id);
        Assert.Equal(EmailProcessingPriority.High, dequeuedEmail.Priority);
    }

    [Fact]
    public async Task GetQueueStatusAsync_ShouldReturnCorrectCounts()
    {
        // Arrange
        var emailAccountId = "account-123";

        _emailMetadataRepositoryMock
            .Setup(x => x.GetByExternalEmailIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailMetadata?)null);

        _emailMetadataRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<EmailMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailMetadata em, CancellationToken ct) => em);

        _emailMetadataRepositoryMock
            .Setup(x => x.GetProcessedCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Queue emails with different priorities
        await _emailQueueService.QueueEmailForProcessingAsync(
            emailAccountId,
            new EmailMessage { Id = "1", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow },
            EmailProcessingPriority.High);

        await _emailQueueService.QueueEmailForProcessingAsync(
            emailAccountId,
            new EmailMessage { Id = "2", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow },
            EmailProcessingPriority.High);

        await _emailQueueService.QueueEmailForProcessingAsync(
            emailAccountId,
            new EmailMessage { Id = "3", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow },
            EmailProcessingPriority.Normal);

        await _emailQueueService.QueueEmailForProcessingAsync(
            emailAccountId,
            new EmailMessage { Id = "4", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow },
            EmailProcessingPriority.Low);

        // Act
        var status = await _emailQueueService.GetQueueStatusAsync();

        // Assert
        Assert.Equal(2, status.HighPriorityCount);
        Assert.Equal(1, status.NormalPriorityCount);
        Assert.Equal(1, status.LowPriorityCount);
        Assert.Equal(4, status.PendingCount);
        Assert.Equal(5, status.ProcessedCount);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_ShouldUpdateEmailMetadata()
    {
        // Arrange
        var emailMetadataId = "metadata-123";
        var subscriptionId = "subscription-456";

        var emailMetadata = new EmailMetadata
        {
            Id = emailMetadataId,
            EmailAccountId = "account-123",
            ExternalEmailId = "email-123",
            Sender = "sender@example.com",
            Subject = "Test",
            ReceivedAt = DateTime.UtcNow,
            IsProcessed = false
        };

        _emailMetadataRepositoryMock
            .Setup(x => x.GetByIdAsync(emailMetadataId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emailMetadata);

        _emailMetadataRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<EmailMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _emailQueueService.MarkAsProcessedAsync(emailMetadataId, subscriptionId);

        // Assert
        _emailMetadataRepositoryMock.Verify(
            x => x.UpdateAsync(It.Is<EmailMetadata>(em =>
                em.Id == emailMetadataId &&
                em.IsProcessed == true &&
                em.ProcessedAt != null &&
                em.SubscriptionId == subscriptionId
            ), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPendingCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var emailAccountId = "account-123";

        _emailMetadataRepositoryMock
            .Setup(x => x.GetByExternalEmailIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailMetadata?)null);

        _emailMetadataRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<EmailMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailMetadata em, CancellationToken ct) => em);

        _emailMetadataRepositoryMock
            .Setup(x => x.GetProcessedCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Queue 3 emails
        await _emailQueueService.QueueEmailForProcessingAsync(
            emailAccountId,
            new EmailMessage { Id = "1", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow },
            EmailProcessingPriority.Normal);

        await _emailQueueService.QueueEmailForProcessingAsync(
            emailAccountId,
            new EmailMessage { Id = "2", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow },
            EmailProcessingPriority.Normal);

        await _emailQueueService.QueueEmailForProcessingAsync(
            emailAccountId,
            new EmailMessage { Id = "3", Sender = "s", Subject = "s", ReceivedAt = DateTime.UtcNow },
            EmailProcessingPriority.Normal);

        // Act
        var pendingCount = await _emailQueueService.GetPendingCountAsync();

        // Assert
        Assert.Equal(3, pendingCount);
    }
}

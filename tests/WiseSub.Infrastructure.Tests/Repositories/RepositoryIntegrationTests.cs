using Microsoft.EntityFrameworkCore;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;
using WiseSub.Infrastructure.Data;
using WiseSub.Infrastructure.Repositories;
using Xunit;

namespace WiseSub.Infrastructure.Tests.Repositories;

/// <summary>
/// Integration tests for repository implementations
/// </summary>
public class RepositoryIntegrationTests : IDisposable
{
    private readonly WiseSubDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IEmailAccountRepository _emailAccountRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IAlertRepository _alertRepository;
    private readonly IVendorMetadataRepository _vendorRepository;

    public RepositoryIntegrationTests()
    {
        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<WiseSubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new WiseSubDbContext(options);
        _userRepository = new UserRepository(_context);
        _emailAccountRepository = new EmailAccountRepository(_context);
        _subscriptionRepository = new SubscriptionRepository(_context);
        _alertRepository = new AlertRepository(_context);
        _vendorRepository = new VendorMetadataRepository(_context);
    }

    [Fact]
    public async Task UserRepository_CreateAndRetrieve_Success()
    {
        // Arrange
        var user = new User
        {
            Email = "test@example.com",
            Name = "Test User",
            OAuthProvider = "Google",
            OAuthSubjectId = "12345",
            Tier = SubscriptionTier.Free
        };

        // Act
        var created = await _userRepository.AddAsync(user);
        var retrieved = await _userRepository.GetByIdAsync(created.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(user.Email, retrieved.Email);
        Assert.Equal(user.Name, retrieved.Name);
    }

    [Fact]
    public async Task UserRepository_GetByEmail_Success()
    {
        // Arrange
        var user = new User
        {
            Email = "unique@example.com",
            Name = "Unique User",
            OAuthProvider = "Google",
            OAuthSubjectId = "67890"
        };
        await _userRepository.AddAsync(user);

        // Act
        var retrieved = await _userRepository.GetByEmailAsync("unique@example.com");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(user.Email, retrieved.Email);
    }

    [Fact]
    public async Task EmailAccountRepository_TokenManagement_Success()
    {
        // Arrange
        var user = new User
        {
            Email = "user@example.com",
            Name = "User",
            OAuthProvider = "Google",
            OAuthSubjectId = "123"
        };
        await _userRepository.AddAsync(user);

        var account = new EmailAccount
        {
            UserId = user.Id,
            EmailAddress = "user@gmail.com",
            Provider = EmailProvider.Gmail,
            EncryptedAccessToken = "encrypted_token",
            EncryptedRefreshToken = "encrypted_refresh",
            TokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        await _emailAccountRepository.AddAsync(account);

        // Act
        await _emailAccountRepository.UpdateTokensAsync(
            account.Id,
            "new_encrypted_token",
            "new_encrypted_refresh",
            DateTime.UtcNow.AddHours(2)
        );

        var updated = await _emailAccountRepository.GetByIdAsync(account.Id);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("new_encrypted_token", updated.EncryptedAccessToken);
        Assert.Equal("new_encrypted_refresh", updated.EncryptedRefreshToken);
    }

    [Fact]
    public async Task SubscriptionRepository_FilteringAndAggregation_Success()
    {
        // Arrange
        var user = new User
        {
            Email = "sub@example.com",
            Name = "Sub User",
            OAuthProvider = "Google",
            OAuthSubjectId = "456"
        };
        await _userRepository.AddAsync(user);

        var account = new EmailAccount
        {
            UserId = user.Id,
            EmailAddress = "sub@gmail.com",
            Provider = EmailProvider.Gmail,
            EncryptedAccessToken = "token",
            EncryptedRefreshToken = "refresh"
        };
        await _emailAccountRepository.AddAsync(account);

        var subscription1 = new Subscription
        {
            UserId = user.Id,
            EmailAccountId = account.Id,
            ServiceName = "Netflix",
            Price = 15.99m,
            Currency = "USD",
            BillingCycle = BillingCycle.Monthly,
            Category = "Entertainment",
            Status = SubscriptionStatus.Active
        };

        var subscription2 = new Subscription
        {
            UserId = user.Id,
            EmailAccountId = account.Id,
            ServiceName = "Spotify",
            Price = 9.99m,
            Currency = "USD",
            BillingCycle = BillingCycle.Monthly,
            Category = "Entertainment",
            Status = SubscriptionStatus.Active
        };

        await _subscriptionRepository.AddAsync(subscription1);
        await _subscriptionRepository.AddAsync(subscription2);

        // Act
        var allSubs = await _subscriptionRepository.GetByUserIdAsync(user.Id);
        var activeSubs = await _subscriptionRepository.GetByUserIdAndStatusAsync(user.Id, SubscriptionStatus.Active);
        var totalSpending = await _subscriptionRepository.GetTotalMonthlySpendingAsync(user.Id);
        var byCategory = await _subscriptionRepository.GetSpendingByCategoryAsync(user.Id);

        // Assert
        Assert.Equal(2, allSubs.Count());
        Assert.Equal(2, activeSubs.Count());
        Assert.Equal(25.98m, totalSpending);
        Assert.True(byCategory.ContainsKey("Entertainment"));
        Assert.Equal(25.98m, byCategory["Entertainment"]);
    }

    [Fact]
    public async Task AlertRepository_SchedulingQueries_Success()
    {
        // Arrange
        var user = new User
        {
            Email = "alert@example.com",
            Name = "Alert User",
            OAuthProvider = "Google",
            OAuthSubjectId = "789"
        };
        await _userRepository.AddAsync(user);

        var account = new EmailAccount
        {
            UserId = user.Id,
            EmailAddress = "alert@gmail.com",
            Provider = EmailProvider.Gmail,
            EncryptedAccessToken = "token",
            EncryptedRefreshToken = "refresh"
        };
        await _emailAccountRepository.AddAsync(account);

        var subscription = new Subscription
        {
            UserId = user.Id,
            EmailAccountId = account.Id,
            ServiceName = "Test Service",
            Price = 10m,
            Currency = "USD",
            BillingCycle = BillingCycle.Monthly,
            Status = SubscriptionStatus.Active
        };
        await _subscriptionRepository.AddAsync(subscription);

        var alert = new Alert
        {
            UserId = user.Id,
            SubscriptionId = subscription.Id,
            Type = AlertType.RenewalUpcoming7Days,
            Message = "Renewal in 7 days",
            ScheduledFor = DateTime.UtcNow.AddDays(-1),
            Status = AlertStatus.Pending
        };
        await _alertRepository.AddAsync(alert);

        // Act
        var pendingAlerts = await _alertRepository.GetPendingAlertsAsync(DateTime.UtcNow);
        await _alertRepository.MarkAsSentAsync(alert.Id);
        var sentAlert = await _alertRepository.GetByIdAsync(alert.Id);

        // Assert
        Assert.Single(pendingAlerts);
        Assert.NotNull(sentAlert);
        Assert.Equal(AlertStatus.Sent, sentAlert.Status);
        Assert.NotNull(sentAlert.SentAt);
    }

    [Fact]
    public async Task VendorMetadataRepository_SearchAndCache_Success()
    {
        // Arrange
        var vendor = new VendorMetadata
        {
            Name = "Netflix",
            NormalizedName = "netflix",
            Category = "Entertainment",
            LogoUrl = "https://example.com/netflix.png",
            WebsiteUrl = "https://netflix.com"
        };
        await _vendorRepository.AddAsync(vendor);

        // Act
        var byName = await _vendorRepository.GetByNormalizedNameAsync("netflix");
        var searchResults = await _vendorRepository.SearchByNameAsync("net");
        var allVendors = await _vendorRepository.GetAllForCacheAsync();

        // Assert
        Assert.NotNull(byName);
        Assert.Equal("Netflix", byName.Name);
        Assert.Single(searchResults);
        Assert.Single(allVendors);
    }

    [Fact]
    public async Task UserRepository_DeleteUserData_CascadesCorrectly()
    {
        // Arrange
        var user = new User
        {
            Email = "delete@example.com",
            Name = "Delete User",
            OAuthProvider = "Google",
            OAuthSubjectId = "999"
        };
        await _userRepository.AddAsync(user);

        var account = new EmailAccount
        {
            UserId = user.Id,
            EmailAddress = "delete@gmail.com",
            Provider = EmailProvider.Gmail,
            EncryptedAccessToken = "token",
            EncryptedRefreshToken = "refresh"
        };
        await _emailAccountRepository.AddAsync(account);

        // Act
        await _userRepository.DeleteUserDataAsync(user.Id);
        var deletedUser = await _userRepository.GetByIdAsync(user.Id);
        var deletedAccount = await _emailAccountRepository.GetByIdAsync(account.Id);

        // Assert
        Assert.Null(deletedUser);
        Assert.Null(deletedAccount);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

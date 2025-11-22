using Microsoft.EntityFrameworkCore;
using WiseSub.Application.Services;
using WiseSub.Domain.Entities;
using WiseSub.Domain.Enums;
using WiseSub.Infrastructure.Data;
using WiseSub.Infrastructure.Repositories;
using Xunit;

namespace WiseSub.Infrastructure.Tests.Services;

public class UserServiceTests : IDisposable
{
    private readonly WiseSubDbContext _context;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<WiseSubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new WiseSubDbContext(options);
        
        // Create repository instances
        var userRepository = new UserRepository(_context);
        var emailAccountRepository = new EmailAccountRepository(_context);
        var subscriptionRepository = new SubscriptionRepository(_context);
        var alertRepository = new AlertRepository(_context);
        
        _userService = new UserService(
            userRepository,
            emailAccountRepository,
            subscriptionRepository,
            alertRepository);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldCreateUserWithFreeTier()
    {
        // Arrange
        var email = "test@example.com";
        var name = "Test User";
        var provider = "Google";
        var subjectId = "google-123";

        // Act
        var user = await _userService.CreateUserAsync(email, name, provider, subjectId);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Equal(name, user.Name);
        Assert.Equal(provider, user.OAuthProvider);
        Assert.Equal(subjectId, user.OAuthSubjectId);
        Assert.Equal(SubscriptionTier.Free, user.Tier);
        Assert.NotNull(user.LastLoginAt);
        Assert.NotEmpty(user.PreferencesJson);
    }

    [Fact]
    public async Task GetUserByIdAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var user = await _userService.CreateUserAsync("test@example.com", "Test User", "Google", "google-123");

        // Act
        var retrievedUser = await _userService.GetUserByIdAsync(user.Id);

        // Assert
        Assert.NotNull(retrievedUser);
        Assert.Equal(user.Id, retrievedUser.Id);
        Assert.Equal(user.Email, retrievedUser.Email);
    }

    [Fact]
    public async Task GetUserByIdAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Act
        var user = await _userService.GetUserByIdAsync("non-existent-id");

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task GetUserByEmailAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var email = "test@example.com";
        await _userService.CreateUserAsync(email, "Test User", "Google", "google-123");

        // Act
        var user = await _userService.GetUserByEmailAsync(email);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
    }

    [Fact]
    public async Task GetUserByOAuthSubjectIdAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var provider = "Google";
        var subjectId = "google-123";
        await _userService.CreateUserAsync("test@example.com", "Test User", provider, subjectId);

        // Act
        var user = await _userService.GetUserByOAuthSubjectIdAsync(provider, subjectId);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(provider, user.OAuthProvider);
        Assert.Equal(subjectId, user.OAuthSubjectId);
    }

    [Fact]
    public async Task UpdateLastLoginAsync_ShouldUpdateLastLoginTime()
    {
        // Arrange
        var user = await _userService.CreateUserAsync("test@example.com", "Test User", "Google", "google-123");
        var originalLastLogin = user.LastLoginAt;
        await Task.Delay(100); // Small delay to ensure time difference

        // Act
        await _userService.UpdateLastLoginAsync(user.Id);

        // Assert
        var updatedUser = await _userService.GetUserByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.NotNull(updatedUser.LastLoginAt);
        Assert.True(updatedUser.LastLoginAt > originalLastLogin);
    }

    [Fact]
    public async Task DeleteUserDataAsync_ShouldRemoveUser()
    {
        // Arrange
        var user = await _userService.CreateUserAsync("test@example.com", "Test User", "Google", "google-123");

        // Act
        await _userService.DeleteUserDataAsync(user.Id);

        // Assert
        var deletedUser = await _userService.GetUserByIdAsync(user.Id);
        Assert.Null(deletedUser);
    }

    [Fact]
    public async Task ExportUserDataAsync_ShouldReturnJsonData()
    {
        // Arrange
        var user = await _userService.CreateUserAsync("test@example.com", "Test User", "Google", "google-123");

        // Act
        var exportData = await _userService.ExportUserDataAsync(user.Id);

        // Assert
        Assert.NotNull(exportData);
        Assert.True(exportData.Length > 0);
        
        var json = System.Text.Encoding.UTF8.GetString(exportData);
        Assert.Contains(user.Email, json);
        Assert.Contains(user.Name, json);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

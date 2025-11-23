using Microsoft.EntityFrameworkCore;
using WiseSub.Application.Services;
using WiseSub.Domain.Common;
using WiseSub.Domain.Enums;
using WiseSub.Infrastructure.Data;
using WiseSub.Infrastructure.Repositories;

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
        var result = await _userService.CreateUserAsync(email, name, provider, subjectId);

        // Assert
        Assert.True(result.IsSuccess);
        var user = result.Value;
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
        var createResult = await _userService.CreateUserAsync("test@example.com", "Test User", "Google", "google-123");
        var user = createResult.Value;

        // Act
        var result = await _userService.GetUserByIdAsync(user.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var retrievedUser = result.Value;
        Assert.NotNull(retrievedUser);
        Assert.Equal(user.Id, retrievedUser.Id);
        Assert.Equal(user.Email, retrievedUser.Email);
    }

    [Fact]
    public async Task GetUserByIdAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Act
        var result = await _userService.GetUserByIdAsync("non-existent-id");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal($"{UserErrors.NotFound.Code}: {UserErrors.NotFound.Message}", result.ErrorMessage);
    }

    [Fact]
    public async Task GetUserByEmailAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var email = "test@example.com";
        await _userService.CreateUserAsync(email, "Test User", "Google", "google-123");

        // Act
        var result = await _userService.GetUserByEmailAsync(email);

        // Assert
        Assert.True(result.IsSuccess);
        var user = result.Value;
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
        var result = await _userService.GetUserByOAuthSubjectIdAsync(provider, subjectId);

        // Assert
        Assert.True(result.IsSuccess);
        var user = result.Value;
        Assert.NotNull(user);
        Assert.Equal(provider, user.OAuthProvider);
        Assert.Equal(subjectId, user.OAuthSubjectId);
    }

    [Fact]
    public async Task UpdateLastLoginAsync_ShouldUpdateLastLoginTime()
    {
        // Arrange
        var createResult = await _userService.CreateUserAsync("test@example.com", "Test User", "Google", "google-123");
        var user = createResult.Value;
        var originalLastLogin = user.LastLoginAt;
        await Task.Delay(100); // Small delay to ensure time difference

        // Act
        var updateResult = await _userService.UpdateLastLoginAsync(user.Id);

        // Assert
        Assert.True(updateResult.IsSuccess);
        var getUserResult = await _userService.GetUserByIdAsync(user.Id);
        Assert.True(getUserResult.IsSuccess);
        var updatedUser = getUserResult.Value;
        Assert.NotNull(updatedUser);
        Assert.NotNull(updatedUser.LastLoginAt);
        Assert.True(updatedUser.LastLoginAt > originalLastLogin);
    }

    [Fact]
    public async Task DeleteUserDataAsync_ShouldRemoveUser()
    {
        // Arrange
        var createResult = await _userService.CreateUserAsync("test@example.com", "Test User", "Google", "google-123");
        var user = createResult.Value;

        // Act
        var deleteResult = await _userService.DeleteUserDataAsync(user.Id);

        // Assert
        Assert.True(deleteResult.IsSuccess);
        var getUserResult = await _userService.GetUserByIdAsync(user.Id);
        Assert.True(getUserResult.IsFailure);
    }

    [Fact]
    public async Task ExportUserDataAsync_ShouldReturnJsonData()
    {
        // Arrange
        var createResult = await _userService.CreateUserAsync("test@example.com", "Test User", "Google", "google-123");
        var user = createResult.Value;

        // Act
        var result = await _userService.ExportUserDataAsync(user.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var exportData = result.Value;
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

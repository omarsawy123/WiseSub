using WiseSub.Domain.Entities;

namespace WiseSub.Application.Common.Interfaces;

public interface IUserService
{
    Task<User> CreateUserAsync(string email, string name, string oauthProvider, string oauthSubjectId);
    Task<User?> GetUserByIdAsync(string userId);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByOAuthSubjectIdAsync(string oauthProvider, string oauthSubjectId);
    Task<User> UpdateUserAsync(User user);
    Task UpdateLastLoginAsync(string userId);
    Task<byte[]> ExportUserDataAsync(string userId);
    Task DeleteUserDataAsync(string userId);
}

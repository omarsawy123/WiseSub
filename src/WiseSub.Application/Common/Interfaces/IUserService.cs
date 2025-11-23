using WiseSub.Domain.Common;
using WiseSub.Domain.Entities;

namespace WiseSub.Application.Common.Interfaces;

public interface IUserService
{
    Task<Result<User>> CreateUserAsync(string email, string name, string oauthProvider, string oauthSubjectId);
    Task<Result<User>> GetUserByIdAsync(string userId);
    Task<Result<User>> GetUserByEmailAsync(string email);
    Task<Result<User>> GetUserByOAuthSubjectIdAsync(string oauthProvider, string oauthSubjectId);
    Task<Result<User>> UpdateUserAsync(User user);
    Task<Result> UpdateLastLoginAsync(string userId);
    Task<Result<byte[]>> ExportUserDataAsync(string userId);
    Task<Result> DeleteUserDataAsync(string userId);
}

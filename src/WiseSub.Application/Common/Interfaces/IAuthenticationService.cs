using WiseSub.Application.Common.Models;

namespace WiseSub.Application.Common.Interfaces;

public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateWithGoogleAsync(string authorizationCode);
    Task<AuthenticationResult> RefreshTokenAsync(string refreshToken);
    Task<bool> RevokeTokenAsync(string refreshToken);
    string GenerateJwtToken(string userId, string email);
}

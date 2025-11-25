using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WiseSub.Application.Common.Interfaces;
using WiseSub.Application.Common.Models;
using WiseSub.Domain.Common;

namespace WiseSub.Infrastructure.Authentication;

public class GoogleAuthenticationService : IAuthenticationService
{
    private readonly IConfiguration _configuration;
    private readonly IUserService _userService;
    private readonly HttpClient _httpClient;

    public GoogleAuthenticationService(
        IConfiguration configuration,
        IUserService userService,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _userService = userService;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<AuthenticationResult> AuthenticateWithGoogleAsync(string authorizationCode)
    {
        // Exchange authorization code for access token
        var tokenResponse = await ExchangeCodeForTokenAsync(authorizationCode);
        if (tokenResponse == null)
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorMessage = AuthenticationErrors.InvalidCredentials.Message
            };
        }

        // Get user info from Google
        var userInfo = await GetGoogleUserInfoAsync(tokenResponse.AccessToken);
        if (userInfo == null)
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorMessage = AuthenticationErrors.InvalidCredentials.Message
            };
        }

        // Check if user exists
        var existingUserResult = await _userService.GetUserByOAuthSubjectIdAsync("Google", userInfo.Sub);
        
        bool isNewUser = false;
        string userId;
        
        if (existingUserResult.IsFailure)
        {
            // Create new user
            var createUserResult = await _userService.CreateUserAsync(
                userInfo.Email,
                userInfo.Name,
                "Google",
                userInfo.Sub
            );
            
            if (createUserResult.IsFailure)
            {
                return new AuthenticationResult
                {
                    Success = false,
                    ErrorMessage = createUserResult.ErrorMessage
                };
            }
            
            userId = createUserResult.Value.Id;
            isNewUser = true;
        }
        else
        {
            // Update last login
            userId = existingUserResult.Value.Id;
            await _userService.UpdateLastLoginAsync(userId);
        }

        // Generate JWT token
        var jwtToken = GenerateJwtToken(userId, userInfo.Email);

        return new AuthenticationResult
        {
            Success = true,
            UserId = userId,
            Email = userInfo.Email,
            JwtToken = jwtToken,
            RefreshToken = tokenResponse.RefreshToken,
            IsNewUser = isNewUser
        };
    }

    public async Task<AuthenticationResult> RefreshTokenAsync(string refreshToken)
    {
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
            new FormUrlEncodedContent(requestData)
        );

        if (!response.IsSuccessStatusCode)
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorMessage = AuthenticationErrors.InvalidToken.Message
            };
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>();
        if (tokenResponse == null)
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorMessage = AuthenticationErrors.InvalidToken.Message
            };
        }

        // Get user info
        var userInfo = await GetGoogleUserInfoAsync(tokenResponse.AccessToken);
        if (userInfo == null)
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorMessage = AuthenticationErrors.InvalidCredentials.Message
            };
        }

        var userResult = await _userService.GetUserByOAuthSubjectIdAsync("Google", userInfo.Sub);
        if (userResult.IsFailure)
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorMessage = UserErrors.NotFound.Message
            };
        }

        var jwtToken = GenerateJwtToken(userResult.Value.Id, userResult.Value.Email);

        return new AuthenticationResult
        {
            Success = true,
            UserId = userResult.Value.Id,
            Email = userResult.Value.Email,
            JwtToken = jwtToken,
            RefreshToken = refreshToken
        };
    }

    public async Task<bool> RevokeTokenAsync(string refreshToken)
    {
        try
        {
            var requestData = new Dictionary<string, string>
            {
                { "token", refreshToken }
            };

            var response = await _httpClient.PostAsync(
                "https://oauth2.googleapis.com/revoke",
                new FormUrlEncodedContent(requestData)
            );

            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // Return false on any error (network failures, etc.)
            return false;
        }
    }

    public string GenerateJwtToken(string userId, string email)
    {
        var jwtSecret = _configuration["Authentication:JwtSecret"] 
            ?? throw new InvalidOperationException("JWT secret not configured");
        var jwtIssuer = _configuration["Authentication:JwtIssuer"] ?? "WiseSub";
        var jwtAudience = _configuration["Authentication:JwtAudience"] ?? "WiseSub";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<GoogleTokenResponse?> ExchangeCodeForTokenAsync(string authorizationCode)
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
            { "grant_type", "authorization_code" }
        };

        var response = await _httpClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(requestData)
        );

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<GoogleTokenResponse>();
    }

    private async Task<GoogleUserInfo?> GetGoogleUserInfoAsync(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");
        
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<GoogleUserInfo>();
    }

    private class GoogleTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = string.Empty;
    }
}

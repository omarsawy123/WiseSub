using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseSub.Application.Common.Interfaces;

namespace WiseSub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IUserService _userService;

    public AuthController(
        IAuthenticationService authenticationService,
        IUserService userService)
    {
        _authenticationService = authenticationService;
        _userService = userService;
    }

    [HttpPost("google")]
    public async Task<IActionResult> AuthenticateWithGoogle([FromBody] GoogleAuthRequest request)
    {
        if (string.IsNullOrEmpty(request.AuthorizationCode))
        {
            return BadRequest(new { error = "Authorization code is required" });
        }

        var result = await _authenticationService.AuthenticateWithGoogleAsync(request.AuthorizationCode);

        if (!result.Success)
        {
            return Unauthorized(new { error = result.ErrorMessage });
        }

        return Ok(new
        {
            userId = result.UserId,
            email = result.Email,
            token = result.JwtToken,
            refreshToken = result.RefreshToken,
            isNewUser = result.IsNewUser
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest(new { error = "Refresh token is required" });
        }

        var result = await _authenticationService.RefreshTokenAsync(request.RefreshToken);

        if (!result.Success)
        {
            return Unauthorized(new { error = result.ErrorMessage });
        }

        return Ok(new
        {
            userId = result.UserId,
            email = result.Email,
            token = result.JwtToken,
            refreshToken = result.RefreshToken
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request)
    {
        // Revoke Google OAuth refresh token if provided
        if (request?.RefreshToken != null)
        {
            await _authenticationService.RevokeTokenAsync(request.RefreshToken);
        }

        // In a JWT-based system, logout is typically handled client-side
        // by removing the token. The Google OAuth refresh token has been revoked.
        return Ok(new { message = "Logged out successfully" });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            name = user.Name,
            tier = user.Tier.ToString(),
            createdAt = user.CreatedAt,
            lastLoginAt = user.LastLoginAt
        });
    }
}

public class GoogleAuthRequest
{
    public string AuthorizationCode { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequest
{
    public string? RefreshToken { get; set; }
}

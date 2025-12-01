using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WiseSub.Application.Common.Interfaces;

namespace WiseSub.API.Controllers;

/// <summary>
/// Controller for authentication operations including Google OAuth and JWT token management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IUserService _userService;

    /// <summary>
    /// Initializes a new instance of the AuthController
    /// </summary>
    public AuthController(
        IAuthenticationService authenticationService,
        IUserService userService)
    {
        _authenticationService = authenticationService;
        _userService = userService;
    }

    /// <summary>
    /// Authenticates a user with Google OAuth authorization code
    /// </summary>
    /// <param name="request">The Google OAuth authorization code</param>
    /// <returns>JWT token and user information on success</returns>
    /// <response code="200">Returns the JWT token and user details</response>
    /// <response code="400">If the authorization code is missing or invalid</response>
    /// <response code="401">If authentication fails</response>
    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    /// <summary>
    /// Refreshes an expired JWT token using a refresh token
    /// </summary>
    /// <param name="request">The refresh token</param>
    /// <returns>New JWT token and refresh token</returns>
    /// <response code="200">Returns new tokens</response>
    /// <response code="400">If the refresh token is missing</response>
    /// <response code="401">If the refresh token is invalid or expired</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    /// <summary>
    /// Logs out the current user and revokes the refresh token
    /// </summary>
    /// <param name="request">Optional refresh token to revoke</param>
    /// <returns>Success message</returns>
    /// <response code="200">Logout successful</response>
    /// <response code="401">If the user is not authenticated</response>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    /// <summary>
    /// Gets the current authenticated user's information
    /// </summary>
    /// <returns>Current user details</returns>
    /// <response code="200">Returns the current user</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="404">If the user is not found</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var userResult = await _userService.GetUserByIdAsync(userId);
        if (userResult.IsFailure)
        {
            return NotFound(new { error = userResult.ErrorMessage });
        }

        var user = userResult.Value;
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

/// <summary>
/// Request model for Google OAuth authentication
/// </summary>
public class GoogleAuthRequest
{
    /// <summary>
    /// The authorization code received from Google OAuth flow
    /// </summary>
    /// <example>4/0AX4XfWh...</example>
    [Required(ErrorMessage = "Authorization code is required")]
    [MinLength(10, ErrorMessage = "Authorization code is too short")]
    public string AuthorizationCode { get; set; } = string.Empty;
}

/// <summary>
/// Request model for refreshing JWT tokens
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// The refresh token issued during authentication
    /// </summary>
    [Required(ErrorMessage = "Refresh token is required")]
    [MinLength(10, ErrorMessage = "Refresh token is too short")]
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Request model for logout operation
/// </summary>
public class LogoutRequest
{
    /// <summary>
    /// Optional refresh token to revoke
    /// </summary>
    public string? RefreshToken { get; set; }
}

/// <summary>
/// Response model for successful authentication
/// </summary>
public class AuthenticationResponse
{
    /// <summary>
    /// The user's unique identifier
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// The user's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// JWT access token for API authentication
    /// </summary>
    public string Token { get; set; } = string.Empty;
    
    /// <summary>
    /// Refresh token for obtaining new access tokens
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates if this is a newly created user
    /// </summary>
    public bool IsNewUser { get; set; }
}

/// <summary>
/// Response model for current user information
/// </summary>
public class CurrentUserResponse
{
    /// <summary>
    /// The user's unique identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// The user's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// The user's display name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The user's subscription tier (Free, Pro, Premium)
    /// </summary>
    public string Tier { get; set; } = string.Empty;
    
    /// <summary>
    /// When the user account was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the user last logged in
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}

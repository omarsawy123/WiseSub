namespace WiseSub.Application.Common.Models;

public class AuthenticationResult
{
    public bool Success { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? JwtToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsNewUser { get; set; }
}

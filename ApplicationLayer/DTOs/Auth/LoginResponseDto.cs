namespace ApplicationLayer.DTOs.Auth;

public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string Role { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// false → frontend redirect đến trang hoàn thiện hồ sơ (OAuth flow, AC-00 SCRUM-147/150).
    /// </summary>
    public bool IsProfileComplete { get; set; }

    /// <summary>
    /// true → user vừa được tạo trong request này (OAuth flow).
    /// FE có thể dùng để hiện màn welcome / hoàn tất hồ sơ.
    /// </summary>
    public bool IsNewUser { get; set; }
}

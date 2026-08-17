namespace ApplicationLayer.DTOs.Auth;

/// <summary>
/// Thông tin tài khoản GitHub trả về sau khi đổi "code" lấy access token (Authorization Code flow).
/// POCO không phụ thuộc HttpClient trực tiếp — để service tier có thể mock, giống GoogleAccountInfo.
/// </summary>
public class GithubAccountInfo
{
    /// <summary>GitHub user id (số, dạng chuỗi) — duy nhất và không đổi kể cả khi user đổi username.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>GitHub username hiện tại (có thể đổi theo thời gian).</summary>
    public string Login { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>URL trang GitHub công khai (vd https://github.com/octocat) — dùng để auto-fill field GithubUrl trong profile.</summary>
    public string ProfileUrl { get; set; } = string.Empty;

    /// <summary>true nếu email lấy được đã được GitHub xác minh (primary + verified trong /user/emails).</summary>
    public bool EmailVerified { get; set; }
}

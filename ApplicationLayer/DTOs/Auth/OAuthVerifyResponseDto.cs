namespace ApplicationLayer.DTOs.Auth;

/// <summary>
/// Trả về sau khi verify Google ID Token. FE dùng để quyết định flow tiếp theo:
/// - isNewUser = true  → hiện màn chọn role + hoàn tất hồ sơ, rồi gọi POST /oauth/google với đủ thông tin.
/// - isNewUser = false → gọi POST /oauth/google chỉ với IdToken để nhận JWT.
/// </summary>
public class OAuthVerifyResponseDto
{
    /// <summary>true nếu chưa có user nào trong DB khớp với Google account này.</summary>
    public bool IsNewUser { get; set; }

    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Picture { get; set; }
    public bool EmailVerified { get; set; }

    /// <summary>
    /// true nếu email này đã tồn tại nhưng được đăng ký bằng Local (password).
    /// FE nên báo user đăng nhập bằng email/password thay vì Google.
    /// </summary>
    public bool LinkedToLocalAccount { get; set; }
}

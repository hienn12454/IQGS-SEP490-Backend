using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Auth;

/// <summary>
/// Request cho bước verify-only của GitHub OAuth flow. Chỉ đổi "code" lấy thông tin tài khoản,
/// không tạo user và không trả JWT. Tương tự OAuthVerifyRequestDto (Google) nhưng GitHub dùng
/// Authorization Code flow (không có ID Token JWT ký sẵn như Google) nên field là Code.
/// </summary>
public class OAuthGithubVerifyRequestDto
{
    /// <summary>Authorization code do GitHub redirect về FE sau khi người dùng cấp quyền.</summary>
    [Required(ErrorMessage = "GitHub authorization code là bắt buộc.")]
    public string Code { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Auth;

/// <summary>
/// Request cho bước verify-only của OAuth flow. Chỉ verify Google ID Token,
/// không tạo user và không trả JWT.
/// </summary>
public class OAuthVerifyRequestDto
{
    [Required(ErrorMessage = "Google ID Token là bắt buộc.")]
    public string IdToken { get; set; } = string.Empty;
}

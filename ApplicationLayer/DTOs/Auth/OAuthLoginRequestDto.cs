using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Auth;

public class OAuthLoginRequestDto
{
    /// <summary>Google ID Token do frontend gửi lên sau khi người dùng đăng nhập Google.</summary>
    [Required(ErrorMessage = "Google ID Token là bắt buộc.")]
    public string IdToken { get; set; } = string.Empty;

    /// <summary>
    /// Vai trò mong muốn khi tạo tài khoản mới qua OAuth (AC-00 SCRUM-147/150).
    /// Bỏ qua nếu tài khoản đã tồn tại. Mặc định: JobSeeker.
    /// </summary>
    public string? IntendedRole { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Auth;

/// <summary>
/// Request cho bước 2 của GitHub OAuth flow: đăng nhập (returning user) hoặc tạo user + profile
/// (new user). Cấu trúc giống hệt OAuthLoginRequestDto (Google) — chỉ khác Code thay cho IdToken.
/// </summary>
public class OAuthGithubLoginRequestDto
{
    /// <summary>Authorization code do frontend gửi lên sau khi người dùng đăng nhập GitHub.</summary>
    [Required(ErrorMessage = "GitHub authorization code là bắt buộc.")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Vai trò mong muốn khi tạo tài khoản mới qua OAuth (AC-00 SCRUM-147/150).
    /// Chấp nhận "HR" hoặc "Candidate" (case-insensitive). Bỏ qua nếu tài khoản đã tồn tại.
    /// </summary>
    public string? IntendedRole { get; set; }

    // ── Thông tin profile khi tạo mới qua OAuth ──────────────────────
    // Bỏ qua nếu user đã tồn tại (returning user).

    /// <summary>HR only — ID công ty đã có trong hệ thống.</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>HR only — Tên công ty mới nếu chưa có (sẽ tự tạo Company).</summary>
    [MaxLength(255)]
    public string? CompanyName { get; set; }

    /// <summary>HR only — Chức danh (Recruiter, HR Manager...).</summary>
    [MaxLength(150)]
    public string? JobTitle { get; set; }

    /// <summary>Candidate only — Vị trí mục tiêu (Backend Dev, Frontend Dev...).</summary>
    [MaxLength(150)]
    public string? TargetRole { get; set; }

    /// <summary>Candidate only — Cấp độ kinh nghiệm (Intern, Junior, Senior...).</summary>
    [MaxLength(50)]
    public string? SeniorityLevel { get; set; }

    /// <summary>Candidate only — Tech stack.</summary>
    public List<string>? TechStack { get; set; }
}

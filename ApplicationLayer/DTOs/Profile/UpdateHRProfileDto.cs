using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Profile;

/// <summary>SCRUM-161 AC-01: Cập nhật hồ sơ HR Manager.</summary>
public class UpdateHRProfileDto
{
    [Required(ErrorMessage = "Họ tên là bắt buộc.")]
    [MaxLength(255)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    // ── Company ──────────────────────────────────────
    /// <summary>ID của công ty đã có trong hệ thống.</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Tên công ty mới nếu chưa tồn tại trong hệ thống (sẽ tự tạo Company).</summary>
    [MaxLength(255)]
    public string? CompanyName { get; set; }

    // ── HR Profile ────────────────────────────────────
    [MaxLength(150)]
    public string? JobTitle { get; set; }

    [MaxLength(500)]
    public string? LinkedInUrl { get; set; }

    public string? Bio { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Profile;

/// <summary>SCRUM-161 AC-01: Cập nhật hồ sơ HR Manager.</summary>
public class UpdateHRProfileDto
{
    [Required(ErrorMessage = "Họ tên là bắt buộc.")]
    [MinLength(2, ErrorMessage = "Họ tên phải có ít nhất 2 ký tự.")]
    [MaxLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [RegularExpression(@"^0\d{9}$",
        ErrorMessage = "Số điện thoại không hợp lệ. Phải bắt đầu bằng số 0 và có đúng 10 chữ số.")]
    public string? PhoneNumber { get; set; }

    [MaxLength(500, ErrorMessage = "Avatar URL không được vượt quá 500 ký tự.")]
    [Url(ErrorMessage = "Avatar URL không hợp lệ.")]
    public string? AvatarUrl { get; set; }

    /// <summary>ID của công ty đã có trong hệ thống.</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Tên công ty mới nếu chưa tồn tại trong hệ thống (sẽ tự tạo Company).</summary>
    [MinLength(2, ErrorMessage = "Tên công ty phải có ít nhất 2 ký tự.")]
    [MaxLength(255, ErrorMessage = "Tên công ty không được vượt quá 255 ký tự.")]
    public string? CompanyName { get; set; }

    [MaxLength(150, ErrorMessage = "Chức danh không được vượt quá 150 ký tự.")]
    public string? JobTitle { get; set; }

    [MaxLength(500, ErrorMessage = "LinkedIn URL không được vượt quá 500 ký tự.")]
    [Url(ErrorMessage = "LinkedIn URL không hợp lệ.")]
    public string? LinkedInUrl { get; set; }

    [MaxLength(1000, ErrorMessage = "Giới thiệu bản thân không được vượt quá 1000 ký tự.")]
    public string? Bio { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Auth;

/// <summary>SCRUM-147: Đăng ký tài khoản HR Manager.</summary>
public class RegisterHRRequestDto
{
    [Required(ErrorMessage = "Họ tên là bắt buộc.")]
    [MaxLength(255)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc.")]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>ID công ty đã có trong hệ thống (HR chọn từ list).</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Tên công ty mới nếu chưa có (sẽ tự tạo Company mới).</summary>
    [MaxLength(255)]
    public string? CompanyName { get; set; }

    [MaxLength(150)]
    public string? JobTitle { get; set; }
}

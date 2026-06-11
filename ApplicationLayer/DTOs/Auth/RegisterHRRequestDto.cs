using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Auth;

/// <summary>SCRUM-147: Đăng ký tài khoản HR Manager.</summary>
public class RegisterHRRequestDto
{
    [Required(ErrorMessage = "Họ tên là bắt buộc.")]
    [MinLength(2, ErrorMessage = "Họ tên phải có ít nhất 2 ký tự.")]
    [MaxLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(255, ErrorMessage = "Email không được vượt quá 255 ký tự.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    [MaxLength(100, ErrorMessage = "Mật khẩu không được vượt quá 100 ký tự.")]
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d\s])\S{8,}$",
        ErrorMessage = "Mật khẩu phải có ít nhất 1 chữ hoa, 1 chữ thường, 1 số và 1 ký tự đặc biệt.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc.")]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>ID công ty đã có trong hệ thống (HR chọn từ list).</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Tên công ty mới nếu chưa có (sẽ tự tạo Company mới).</summary>
    [MinLength(2, ErrorMessage = "Tên công ty phải có ít nhất 2 ký tự.")]
    [MaxLength(255, ErrorMessage = "Tên công ty không được vượt quá 255 ký tự.")]
    public string? CompanyName { get; set; }

    [MaxLength(150, ErrorMessage = "Chức danh không được vượt quá 150 ký tự.")]
    public string? JobTitle { get; set; }
}

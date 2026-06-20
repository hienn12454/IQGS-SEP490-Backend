using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Auth;

/// <summary>SCRUM-150: Đăng ký tài khoản Candidate.</summary>
public class RegisterCandidateRequestDto
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

    [Required(ErrorMessage = "Vị trí mục tiêu là bắt buộc.")]
    [MaxLength(150, ErrorMessage = "Vị trí mục tiêu không được vượt quá 150 ký tự.")]
    public string TargetRole { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cấp độ kinh nghiệm là bắt buộc.")]
    [MaxLength(50, ErrorMessage = "Cấp độ kinh nghiệm không được vượt quá 50 ký tự.")]
    public string SeniorityLevel { get; set; } = string.Empty;

    public List<string> TechStack { get; set; } = new();
}

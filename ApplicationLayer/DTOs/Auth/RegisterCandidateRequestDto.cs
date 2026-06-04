using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Auth;

/// <summary>SCRUM-150: Đăng ký tài khoản Candidate.</summary>
public class RegisterCandidateRequestDto
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

    [Required(ErrorMessage = "Vị trí mục tiêu là bắt buộc.")]
    [MaxLength(150)]
    public string TargetRole { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cấp độ kinh nghiệm là bắt buộc.")]
    [MaxLength(50)]
    public string SeniorityLevel { get; set; } = string.Empty;

    public List<string> TechStack { get; set; } = new();
}

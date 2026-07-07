using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Auth;

/// <summary>SCRUM-151: Xác minh email bằng mã OTP 6 chữ số.</summary>
public class VerifyEmailDto
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mã xác minh là bắt buộc.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã xác minh phải gồm 6 chữ số.")]
    public string Otp { get; set; } = string.Empty;
}

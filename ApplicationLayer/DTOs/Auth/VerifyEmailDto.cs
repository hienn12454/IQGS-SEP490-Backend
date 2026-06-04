using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Auth;

/// <summary>SCRUM-151: Xác minh email qua token từ link.</summary>
public class VerifyEmailDto
{
    [Required(ErrorMessage = "Token xác minh là bắt buộc.")]
    public string Token { get; set; } = string.Empty;
}

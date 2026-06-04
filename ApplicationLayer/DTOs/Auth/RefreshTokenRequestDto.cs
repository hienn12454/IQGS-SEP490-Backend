using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Auth;

public class RefreshTokenRequestDto
{
    [Required(ErrorMessage = "Refresh token là bắt buộc.")]
    public string RefreshToken { get; set; } = string.Empty;
}

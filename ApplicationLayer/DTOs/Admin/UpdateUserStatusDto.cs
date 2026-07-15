using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Admin;

/// <summary>SCRUM-163 AC-04: Vô hiệu hóa / kích hoạt tài khoản.</summary>
public class UpdateUserStatusDto
{
    [Required(ErrorMessage = "Trạng thái IsActive là bắt buộc.")]
    public bool IsActive { get; set; }
}

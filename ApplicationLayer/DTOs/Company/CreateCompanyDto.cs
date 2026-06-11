using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Company;

public class CreateCompanyDto
{
    [Required(ErrorMessage = "Tên công ty là bắt buộc.")]
    [MinLength(2, ErrorMessage = "Tên công ty phải có ít nhất 2 ký tự.")]
    [MaxLength(255, ErrorMessage = "Tên công ty không được vượt quá 255 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Logo URL không được vượt quá 500 ký tự.")]
    [Url(ErrorMessage = "Logo URL không hợp lệ.")]
    public string? LogoUrl { get; set; }

    [MaxLength(500, ErrorMessage = "Website URL không được vượt quá 500 ký tự.")]
    [Url(ErrorMessage = "Website URL không hợp lệ.")]
    public string? WebsiteUrl { get; set; }

    [MaxLength(2000, ErrorMessage = "Mô tả không được vượt quá 2000 ký tự.")]
    public string? Description { get; set; }
}

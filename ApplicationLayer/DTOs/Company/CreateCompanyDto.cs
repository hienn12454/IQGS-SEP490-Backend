using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Company;

public class CreateCompanyDto
{
    [Required(ErrorMessage = "Tên công ty là bắt buộc.")]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Url(ErrorMessage = "Logo URL không hợp lệ.")]
    public string? LogoUrl { get; set; }

    [MaxLength(500)]
    [Url(ErrorMessage = "Website URL không hợp lệ.")]
    public string? WebsiteUrl { get; set; }

    public string? Description { get; set; }
}

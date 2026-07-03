using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Company;

/// <summary>Tạo nhiều công ty trong 1 request — client bấm dấu + để thêm form rồi gửi cả danh sách.</summary>
public class BulkCreateCompaniesDto
{
    /// <summary>Danh sách công ty cần tạo — mỗi phần tử validate y hệt API tạo lẻ.</summary>
    [Required(ErrorMessage = "Danh sách công ty là bắt buộc.")]
    [MinLength(1, ErrorMessage = "Danh sách công ty phải có ít nhất 1 phần tử.")]
    [MaxLength(50, ErrorMessage = "Chỉ tạo được tối đa 50 công ty mỗi lần.")]
    public List<CreateCompanyDto> Companies { get; set; } = new();
}

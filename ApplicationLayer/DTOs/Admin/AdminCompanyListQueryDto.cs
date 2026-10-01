namespace ApplicationLayer.DTOs.Admin;

/// <summary>SCRUM-480: Query phân trang danh sách công ty (Admin).</summary>
public class AdminCompanyListQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Keyword { get; set; }
}

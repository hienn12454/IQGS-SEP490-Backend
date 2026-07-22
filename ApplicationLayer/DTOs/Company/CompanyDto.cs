namespace ApplicationLayer.DTOs.Company;

public class CompanyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Luôn có giá trị: logo thật nếu công ty đã upload, không thì fallback theo domain website hoặc tự vẽ từ tên công ty.</summary>
    public string? LogoUrl { get; set; }

    public string? WebsiteUrl { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

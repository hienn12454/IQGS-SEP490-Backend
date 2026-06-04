namespace DomainLayer.Entities;

/// <summary>
/// Thông tin công ty của HR. Một công ty có thể có nhiều HR account.
/// Brand công ty hiển thị trên marketplace cho Candidate.
/// </summary>
public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Description { get; set; }

    // Navigation
    public ICollection<HRProfile> HRProfiles { get; set; } = new List<HRProfile>();
}

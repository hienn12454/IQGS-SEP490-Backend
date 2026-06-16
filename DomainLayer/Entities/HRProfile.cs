namespace DomainLayer.Entities;

/// <summary>
/// Thông tin riêng của HR Manager. 1-1 với Users, FK → Companies.
/// </summary>
public class HRProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string? JobTitle { get; set; }               // Chức danh: Recruiter, HR Manager...
    public string? PhoneNumber { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Bio { get; set; }

    /// <summary>Admin xác minh người dùng thực sự thuộc công ty (AC-07 SCRUM-147).</summary>
    public bool IsCompanyVerified { get; set; } = false;
}

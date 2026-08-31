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
    public string? GithubUrl { get; set; }
    public string? Bio { get; set; }

    /// <summary>Admin xác minh người dùng thực sự thuộc công ty (AC-07 SCRUM-147).</summary>
    public bool IsCompanyVerified { get; set; } = false;

    /// <summary>Template lời mời — placeholder {{name}} {{title}} {{score}}.</summary>
    public string? InviteMessageTemplate { get; set; }

    /// <summary>SCRUM-424: MinScore mặc định khi mở list recommendation — null = không lọc.</summary>
    public double? RecDefaultMinScore { get; set; }

    /// <summary>SCRUM-424: Sort mặc định — score | date.</summary>
    public string RecDefaultSortBy { get; set; } = "score";

    /// <summary>SCRUM-424: Hướng sort mặc định — asc | desc.</summary>
    public string RecDefaultSortDir { get; set; } = "desc";

    /// <summary>SCRUM-424: Ẩn tab/status DISMISSED khỏi mặc định list (FE init filter).</summary>
    public bool RecHideDismissed { get; set; }
}

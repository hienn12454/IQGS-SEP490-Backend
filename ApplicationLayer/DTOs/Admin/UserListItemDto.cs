namespace ApplicationLayer.DTOs.Admin;

/// <summary>SCRUM-163 AC-01: Dòng trong bảng danh sách user.</summary>
public class UserListItemDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsProfileComplete { get; set; }
    public string Provider { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>Mã gói subscription (HR_FREE / CANDIDATE_PREMIUM…). Null nếu chưa có sub / role Admin.</summary>
    public string? PlanCode { get; set; }

    /// <summary>True nếu đang Premium và còn trong kỳ (PeriodEnd &gt; now).</summary>
    public bool IsPremium { get; set; }
}

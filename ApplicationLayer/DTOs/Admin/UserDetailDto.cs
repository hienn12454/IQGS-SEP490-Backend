using ApplicationLayer.DTOs.Profile;

namespace ApplicationLayer.DTOs.Admin;

/// <summary>SCRUM-163 AC-03: Chi tiết đầy đủ của một user (chỉ đọc).</summary>
public class UserDetailDto : UserListItemDto
{
    public string? PhoneNumber { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Role-specific profiles (null nếu không có)
    public HRProfileDto? HRProfile { get; set; }
    public CandidateProfileDto? CandidateProfile { get; set; }
}

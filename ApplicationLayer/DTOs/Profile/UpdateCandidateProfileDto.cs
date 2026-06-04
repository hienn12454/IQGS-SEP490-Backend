using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Profile;

/// <summary>SCRUM-161 AC-02: Cập nhật hồ sơ Candidate.</summary>
public class UpdateCandidateProfileDto
{
    [Required(ErrorMessage = "Họ tên là bắt buộc.")]
    [MaxLength(255)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    [MaxLength(150)]
    public string? TargetRole { get; set; }

    [MaxLength(50)]
    public string? SeniorityLevel { get; set; }

    public List<string> TechStack { get; set; } = new();

    [MaxLength(500)]
    public string? LinkedInUrl { get; set; }

    [MaxLength(500)]
    public string? GithubUrl { get; set; }

    public string? Bio { get; set; }
}

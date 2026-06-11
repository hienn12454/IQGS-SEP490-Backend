using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Profile;

/// <summary>SCRUM-161 AC-02: Cập nhật hồ sơ Candidate.</summary>
public class UpdateCandidateProfileDto
{
    [Required(ErrorMessage = "Họ tên là bắt buộc.")]
    [MinLength(2, ErrorMessage = "Họ tên phải có ít nhất 2 ký tự.")]
    [MaxLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [RegularExpression(@"^0\d{9}$",
        ErrorMessage = "Số điện thoại không hợp lệ. Phải bắt đầu bằng số 0 và có đúng 10 chữ số.")]
    public string? PhoneNumber { get; set; }

    [MaxLength(500, ErrorMessage = "Avatar URL không được vượt quá 500 ký tự.")]
    [Url(ErrorMessage = "Avatar URL không hợp lệ.")]
    public string? AvatarUrl { get; set; }

    [MaxLength(150, ErrorMessage = "Vị trí mục tiêu không được vượt quá 150 ký tự.")]
    public string? TargetRole { get; set; }

    [MaxLength(50, ErrorMessage = "Cấp độ kinh nghiệm không được vượt quá 50 ký tự.")]
    public string? SeniorityLevel { get; set; }

    public List<string> TechStack { get; set; } = new();

    [MaxLength(500, ErrorMessage = "LinkedIn URL không được vượt quá 500 ký tự.")]
    [Url(ErrorMessage = "LinkedIn URL không hợp lệ.")]
    public string? LinkedInUrl { get; set; }

    [MaxLength(500, ErrorMessage = "GitHub URL không được vượt quá 500 ký tự.")]
    [Url(ErrorMessage = "GitHub URL không hợp lệ.")]
    public string? GithubUrl { get; set; }

    [MaxLength(1000, ErrorMessage = "Giới thiệu bản thân không được vượt quá 1000 ký tự.")]
    public string? Bio { get; set; }
}

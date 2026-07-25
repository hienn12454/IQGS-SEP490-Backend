namespace ApplicationLayer.DTOs.Profile;

public class ProfileResponseDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;       // "Admin" | "HR" | "Candidate"
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsProfileComplete { get; set; }
    public string Provider { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Role-specific (null nếu không phải role đó)
    public HRProfileDto? HRProfile { get; set; }
    public CandidateProfileDto? CandidateProfile { get; set; }
}

public class HRProfileDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;  // đọc từ Company navigation

    /// <summary>Luôn có giá trị: logo thật nếu công ty đã upload, không thì fallback theo domain website hoặc tự vẽ từ tên công ty.</summary>
    public string CompanyLogo { get; set; } = string.Empty;

    public string? JobTitle { get; set; }
    public string? PhoneNumber { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? GithubUrl { get; set; }
    public string? Bio { get; set; }
    public bool IsCompanyVerified { get; set; }
}

public class CandidateProfileDto
{
    public string? TargetRole { get; set; }
    public string? SeniorityLevel { get; set; }
    public List<string> TechStack { get; set; } = new();
    public string? PhoneNumber { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? GithubUrl { get; set; }
    public string? Bio { get; set; }
    public string? Address { get; set; }
    public string? CvFileName { get; set; }
    public DateTime? CvUploadedAt { get; set; }

    /// <summary>Có đang bật tự đồng bộ thông tin cá nhân từ CV vào profile hay không — đổi qua PUT /api/candidate/cv/sync-settings.</summary>
    public bool AutoSyncProfileFromCv { get; set; }
}

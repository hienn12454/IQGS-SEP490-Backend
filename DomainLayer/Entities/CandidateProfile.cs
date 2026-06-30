namespace DomainLayer.Entities;

/// <summary>
/// Thông tin riêng của Candidate / Job Seeker. 1-1 với Users.
/// (DB table: CandidateProfiles — thay thế JobSeekerProfiles)
/// </summary>
public class CandidateProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string? TargetRole { get; set; }             // Vị trí hướng tới: Backend Dev, Frontend Dev...
    public string? SeniorityLevel { get; set; }         // Intern | Fresher | Junior | Middle | Senior
    public string[] TechStack { get; set; } = Array.Empty<string>();
    public string? PhoneNumber { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? GithubUrl { get; set; }
    public string? Bio { get; set; }

    public string? CvFileName { get; set; }
    public string? CvBlobPath { get; set; }
    public string? CvContentType { get; set; }
    public DateTime? CvUploadedAt { get; set; }

    /// <summary>Thời điểm RAG parse-cv thành công lần gần nhất — null nếu chưa từng parse hoặc lần parse gần nhất thất bại.</summary>
    public DateTime? CvParsedAt { get; set; }

    /// <summary>Kết quả đánh giá CV từ RAG (skills[], summary) dạng JSON.</summary>
    public string? CvEvaluationJson { get; set; }

    /// <summary>Consent cho phép hệ thống đề xuất hồ sơ này cho HR khi đạt điều kiện (SCRUM-293) — mặc định tắt.</summary>
    public bool AllowRecruiterRecommendation { get; set; }
}

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
    public string? Address { get; set; }

    /// <summary>
    /// IANA timezone id (vd "Asia/Ho_Chi_Minh") candidate tự chọn — dùng để quy đổi LocalDate cho
    /// streak/DailyProgress (xem ApplicationLayer.Services.Gamification.UserLocalDateProvider).
    /// Null = chưa chọn, hệ thống fallback UTC.
    /// </summary>
    public string? TimeZoneId { get; set; }

    public string? CvFileName { get; set; }
    public string? CvBlobPath { get; set; }
    public string? CvContentType { get; set; }
    public DateTime? CvUploadedAt { get; set; }

    /// <summary>Thời điểm RAG parse-cv thành công lần gần nhất — null nếu chưa từng parse hoặc lần parse gần nhất thất bại.</summary>
    public DateTime? CvParsedAt { get; set; }

    /// <summary>Kết quả đánh giá CV từ RAG (skills[], summary) dạng JSON.</summary>
    public string? CvEvaluationJson { get; set; }

    /// <summary>Consent cho phép hệ thống đề xuất hồ sơ này cho HR khi đạt điều kiện (SCRUM-293) — mặc định bật.</summary>
    public bool AllowRecruiterRecommendation { get; set; } = true;

    /// <summary>
    /// Bật: mỗi lần upload CV mới, hệ thống tự ghi đè họ tên/SĐT/địa chỉ/GitHub/LinkedIn trên profile
    /// bằng dữ liệu CV vừa trích xuất được — trừ field nào đã có trong <see cref="CvSyncLockedFields"/>
    /// (candidate đã tự tay chỉnh field đó, CV không còn quyền ghi đè nữa). Field CV không trích được thì
    /// giữ nguyên giá trị cũ. Tắt: upload CV chỉ cập nhật TechStack như cũ, không đụng field cá nhân nào.
    /// Mặc định bật.
    /// </summary>
    public bool AutoSyncProfileFromCv { get; set; } = true;

    /// <summary>
    /// Tên các field (theo <see cref="Constants.CvSyncableProfileFields"/>) mà candidate đã tự tay chỉnh
    /// qua PUT profile — CV ưu tiên ghi đè mọi field khi mới có dữ liệu, NHƯNG một khi candidate đã tự sửa
    /// field nào thì field đó bị khóa vĩnh viễn khỏi CV sync (kể cả khi AutoSyncProfileFromCv đang bật).
    /// </summary>
    public string[] CvSyncLockedFields { get; set; } = Array.Empty<string>();
}

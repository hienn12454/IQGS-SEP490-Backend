using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Recommendation;

/// <summary>Query danh sách recommendation trên dashboard HR (SCRUM-291 / SCRUM-328).</summary>
public class HrRecommendationListQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Lọc theo status: NEW | SHORTLISTED | DISMISSED | INVITED — bỏ trống lấy tất cả.</summary>
    public string? Status { get; set; }

    /// <summary>Chỉ lấy recommendation thuộc bộ câu hỏi này.</summary>
    public Guid? QuestionSetId { get; set; }

    /// <summary>Lọc điểm tối thiểu (0–100). Bỏ trống = không lọc.</summary>
    public double? MinScore { get; set; }

    /// <summary>Sắp xếp theo: score (default) | date.</summary>
    public string SortBy { get; set; } = "score";

    /// <summary>Hướng sắp xếp: desc (default) | asc.</summary>
    public string SortDir { get; set; } = "desc";
}

/// <summary>1 candidate được đề xuất trên dashboard HR.</summary>
public class HrRecommendationListItemDto
{
    public Guid Id { get; set; }
    public Guid CandidateUserId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string? TargetRole { get; set; }
    public string? SeniorityLevel { get; set; }
    public List<string> TechStack { get; set; } = new();
    public Guid QuestionSetId { get; set; }
    public string QuestionSetTitle { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>Lý do đề xuất (tính sẵn từ score — không lưu DB, không gọi AI).</summary>
    public string RecommendationReason { get; set; } = string.Empty;

    /// <summary>Trạng thái lời mời nếu đã invite (PENDING/ACCEPTED/REJECTED) — null nếu chưa mời.</summary>
    public string? InvitationStatus { get; set; }

    /// <summary>Lời nhắn candidate gửi kèm khi accept lời mời — null nếu chưa accept hoặc không nhập.</summary>
    public string? InvitationResponseMessage { get; set; }

    /// <summary>SĐT candidate chủ động chia sẻ khi accept lời mời — null nếu chưa accept hoặc không nhập. KHÔNG phải PhoneNumber trên CandidateProfile.</summary>
    public string? InvitationSharedPhoneNumber { get; set; }

    public DateTime RecommendedAt { get; set; }
}

/// <summary>
/// Chi tiết recommendation cho HR (SCRUM-377) — kế thừa list fields + profile/CV/stats.
/// List endpoint vẫn dùng <see cref="HrRecommendationListItemDto"/> để giữ payload nhẹ.
/// </summary>
public class HrRecommendationDetailDto : HrRecommendationListItemDto
{
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? GithubUrl { get; set; }

    public bool HasCv { get; set; }
    public string? CvFileName { get; set; }
    public DateTime? CvUploadedAt { get; set; }
    public string? CvSummary { get; set; }
    public List<string> CvSkills { get; set; } = new();

    public int TotalSessions { get; set; }
    public double? AverageScore { get; set; }
    public double? BestScore { get; set; }

    /// <summary>Có ít nhất 1 phiên hoàn thành ≤ 35 phút — dùng FE render badge Speed Demon.</summary>
    public bool HasFastSession { get; set; }
}

/// <summary>Response GET /api/hr/recommendations/{id}/cv — SAS tạm thời để HR tải CV (SCRUM-377).</summary>
public class HrRecommendationCvDto
{
    public string CvFileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public DateTime? UploadedAt { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}

/// <summary>Read-model 1 dòng recommendation join Users/CandidateProfiles/question_sets — dùng nội bộ bởi repository.</summary>
public class HrRecommendationRow
{
    public Guid Id { get; set; }
    public Guid CandidateUserId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string? TargetRole { get; set; }
    public string? SeniorityLevel { get; set; }
    public string[] TechStack { get; set; } = Array.Empty<string>();
    public Guid QuestionSetId { get; set; }
    public string? QuestionSetTitle { get; set; }
    public double OverallScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? InvitationStatus { get; set; }
    public string? InvitationResponseMessage { get; set; }
    public string? InvitationSharedPhoneNumber { get; set; }
    public DateTime RecommendedAt { get; set; }

    // --- Detail-only fields (SCRUM-377) — list projection để null/default ---
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? GithubUrl { get; set; }
    public string? CvFileName { get; set; }
    public string? CvBlobPath { get; set; }
    public string? CvContentType { get; set; }
    public DateTime? CvUploadedAt { get; set; }
    public string? CvEvaluationJson { get; set; }
}

public class RecommendationActionResponseDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class InviteCandidateRequestDto
{
    /// <summary>Lời nhắn gửi kèm lời mời (tùy chọn, tối đa 2000 ký tự).</summary>
    [MaxLength(2000, ErrorMessage = "Lời nhắn tối đa 2000 ký tự.")]
    public string? Message { get; set; }
}

public class InviteCandidateResponseDto
{
    public Guid RecommendationId { get; set; }
    public Guid InvitationId { get; set; }
    public string RecommendationStatus { get; set; } = string.Empty;
    public string InvitationStatus { get; set; } = string.Empty;
}

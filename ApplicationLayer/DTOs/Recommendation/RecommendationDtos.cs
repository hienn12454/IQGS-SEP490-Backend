using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Recommendation;

/// <summary>Query danh sách recommendation trên dashboard HR (SCRUM-291).</summary>
public class HrRecommendationListQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Lọc theo status: NEW | SHORTLISTED | DISMISSED | INVITED — bỏ trống lấy tất cả.</summary>
    public string? Status { get; set; }

    /// <summary>Chỉ lấy recommendation thuộc bộ câu hỏi này.</summary>
    public Guid? QuestionSetId { get; set; }
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

    /// <summary>Trạng thái lời mời nếu đã invite (PENDING/ACCEPTED/REJECTED) — null nếu chưa mời.</summary>
    public string? InvitationStatus { get; set; }

    public DateTime RecommendedAt { get; set; }
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
    public DateTime RecommendedAt { get; set; }
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

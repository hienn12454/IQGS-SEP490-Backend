using ApplicationLayer.DTOs.Candidate;

namespace ApplicationLayer.DTOs.Hr;

public class HrTalentListQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public Guid? QuestionSetId { get; set; }
    public string? Status { get; set; }
    public double? MinScore { get; set; }
}

public class HrTalentItemDto
{
    public Guid SessionId { get; set; }
    public Guid CandidateUserId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string? TargetRole { get; set; }
    public string? SeniorityLevel { get; set; }
    public Guid QuestionSetId { get; set; }
    public string QuestionSetTitle { get; set; } = string.Empty;
    public string SessionStatus { get; set; } = string.Empty;
    public double? OverallScore { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? RecommendationId { get; set; }
    public string? RecommendationStatus { get; set; }
    public string? InvitationStatus { get; set; }
}

public class HrTalentRow
{
    public Guid SessionId { get; set; }
    public Guid CandidateUserId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string? TargetRole { get; set; }
    public string? SeniorityLevel { get; set; }
    public Guid QuestionSetId { get; set; }
    public string QuestionSetTitle { get; set; } = string.Empty;
    public string SessionStatus { get; set; } = string.Empty;
    public double? OverallScore { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? RecommendationId { get; set; }
    public string? RecommendationStatus { get; set; }
    public string? InvitationStatus { get; set; }
}

namespace DomainLayer.Entities;

public class PracticeSession : BaseEntity
{
    public Guid CandidateUserId { get; set; }
    public Guid QuestionSetId { get; set; }
    public string Status { get; set; } = Constants.PracticeSessionStatus.InProgress;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? OverallScore { get; set; }

    /// <summary>Nhận xét AI tổng quan (tiếng Việt) — SCRUM-305.</summary>
    public string? AiInsightVi { get; set; }

    /// <summary>Nhận xét AI tổng quan (tiếng Anh) — SCRUM-305.</summary>
    public string? AiInsightEn { get; set; }

    /// <summary>JSON {"vi":[...],"en":[...]} — kỹ năng cần cải thiện (SCRUM-305).</summary>
    public string? SkillsToImproveJson { get; set; }

    public QuestionSet QuestionSet { get; set; } = null!;
}

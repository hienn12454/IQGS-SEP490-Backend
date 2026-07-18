namespace DomainLayer.Entities;

/// <summary>
/// Recommendation rule-based (SCRUM-291): candidate hoàn thành phiên luyện tập đạt điểm cao
/// trên bộ câu hỏi PUBLISHED và đã bật consent → hệ thống tự đề xuất hồ sơ cho HR sở hữu bộ đó.
/// Unique theo (CandidateUserId, QuestionSetId) — làm lại nhiều lần chỉ cập nhật điểm cao nhất.
/// </summary>
public class CandidateRecommendation : BaseEntity
{
    public Guid CandidateUserId { get; set; }
    public Guid QuestionSetId { get; set; }

    /// <summary>Phiên luyện tập đạt điểm cao nhất đã kích hoạt recommendation này.</summary>
    public Guid PracticeSessionId { get; set; }

    /// <summary>HR sở hữu bộ câu hỏi — denormalize để query dashboard theo HR không phải join question_sets.</summary>
    public Guid HrOwnerId { get; set; }

    /// <summary>Snapshot OverallScore tại thời điểm đề xuất (thang 0–100, luôn ≥ ngưỡng rule).</summary>
    public double OverallScore { get; set; }

    public string Status { get; set; } = Constants.CandidateRecommendationStatus.New;

    public QuestionSet QuestionSet { get; set; } = null!;
    public PracticeSession PracticeSession { get; set; } = null!;
}

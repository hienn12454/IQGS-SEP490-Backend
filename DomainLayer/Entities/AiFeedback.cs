namespace DomainLayer.Entities;

/// <summary>Phản hồi AI cho 1 candidate_answer — quan hệ 1-1 (SCRUM-282).</summary>
public class AiFeedback : BaseEntity
{
    public Guid CandidateAnswerId { get; set; }
    public double? Score { get; set; }
    public string StrengthsJson { get; set; } = "[]";
    public string ImprovementsJson { get; set; } = "[]";
    public string? Suggestion { get; set; }
    public string? DimensionScoresJson { get; set; }
    /// <summary>
    /// Điểm từng tiêu chí rubric HR [{code,label,weight,score}]. Null = câu này chấm tổng thể (không có rubric hợp lệ / Coach / dữ liệu cũ).
    /// </summary>
    public string? CriteriaScoresJson { get; set; }
    public string EvaluationStatus { get; set; } = Constants.AiFeedbackEvaluationStatus.Failed;
    public string? ErrorMessage { get; set; }

    public CandidateAnswer CandidateAnswer { get; set; } = null!;
}

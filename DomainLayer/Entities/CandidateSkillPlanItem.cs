namespace DomainLayer.Entities;

public class CandidateSkillPlanItem : BaseEntity
{
    public Guid PlanId { get; set; }
    public string Skill { get; set; } = string.Empty;
    public double? BaselineScore { get; set; }
    public double? CurrentScore { get; set; }
    /// <summary>Luôn được set từ CompetencyFrameworkSkill.TargetScore; 70 chỉ là giá trị lịch sử cho row cũ.</summary>
    public double TargetScore { get; set; } = 70;
    public string Status { get; set; } = Constants.CandidateSkillPlanItemStatus.Pending;
    public Guid? LastSessionId { get; set; }

    /// <summary>Bậc khó cao nhất ứng viên đã chứng minh được ở skill này (easy/medium/hard).</summary>
    public string? DemonstratedDifficulty { get; set; }
    /// <summary>Trọng số của skill trong framework — snapshot để tính Overall trên profile.</summary>
    public double ImportanceWeight { get; set; }
    /// <summary>Assessment đã cập nhật skill này lần cuối (truy vết evidence).</summary>
    public Guid? SourceAssessmentId { get; set; }
    /// <summary>Diagnostic | Reassessment — nguồn cập nhật lần cuối.</summary>
    public string? UpdatedFromKind { get; set; }
    /// <summary>SCRUM-457: framework | rag — nguồn competency của skill này.</summary>
    public string? SourceMode { get; set; }

    public CandidateSkillPlan Plan { get; set; } = null!;
    public PracticeSession? LastSession { get; set; }
}

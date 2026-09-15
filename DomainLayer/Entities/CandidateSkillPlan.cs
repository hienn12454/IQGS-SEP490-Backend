namespace DomainLayer.Entities;

/// <summary>
/// Competency Profile của ứng viên (SCRUM-454): trạng thái năng lực TÍCH LUỸ theo skill.
/// Diagnostic ghi toàn bộ skill; re-assessment chỉ ghi skill trong phạm vi đo, phần còn lại giữ nguyên.
/// Nhờ vậy Overall/Level luôn tính trên toàn framework thay vì trên một lần đo lẻ.
/// </summary>
public class CandidateSkillPlan : BaseEntity
{
    public Guid CandidateUserId { get; set; }
    public Guid? SourceDiagnosticSetId { get; set; }
    public string Status { get; set; } = Constants.CandidateSkillPlanStatus.Active;

    /// <summary>Framework đang được đo (profile luôn thuộc đúng một framework).</summary>
    public Guid? FrameworkId { get; set; }
    /// <summary>Overall tính lại trên toàn bộ skill của framework sau mỗi lần merge.</summary>
    public double? OverallReadiness { get; set; }
    public string? ReadinessStatus { get; set; }
    /// <summary>Level cao nhất thoả level rule global (Fresher/Junior/Middle/Senior).</summary>
    public string? AchievedLevel { get; set; }
    public Guid? LastAssessmentId { get; set; }

    /// <summary>SCRUM-457: FRAMEWORK | ADAPTIVE — profile theo cùng thang blueprint đang Active.</summary>
    public string? ResolutionMode { get; set; }
    public string? RoleFamilyKey { get; set; }
    public string? ActiveBlueprintJson { get; set; }
    public string? TargetLevel { get; set; }

    public QuestionSet? SourceDiagnosticSet { get; set; }
    public ICollection<CandidateSkillPlanItem> Items { get; set; } = new List<CandidateSkillPlanItem>();
}

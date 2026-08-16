namespace DomainLayer.Entities;

public class CandidatePersonalSetJob : BaseEntity
{
    public Guid CandidateUserId { get; set; }
    public string Status { get; set; } = Constants.CandidatePersonalSetJobStatus.Queued;
    /// <summary>JdGap (mặc định, luồng JD) | CvDiagnostic | CvDrill.</summary>
    public string Purpose { get; set; } = Constants.CandidatePersonalSetPurpose.JdGap;
    public string JobDescription { get; set; } = string.Empty;
    public string CvSkillsJson { get; set; } = "[]";
    public string GapSkillsJson { get; set; } = "[]";
    /// <summary>Drill: JSON mảng 1 skill, vd ["System Design"].</summary>
    public string FocusSkillsJson { get; set; } = "[]";

    public string? PlanJson { get; set; }
    public Guid? QuestionSetId { get; set; }
    public string? ErrorMessage { get; set; }

    public QuestionSet? QuestionSet { get; set; }
}

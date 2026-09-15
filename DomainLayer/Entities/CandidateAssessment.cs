namespace DomainLayer.Entities;

public class CandidateAssessment : BaseEntity
{
    public Guid CandidateUserId { get; set; }
    public Guid? FrameworkId { get; set; }
    public string Kind { get; set; } = Constants.CandidateAssessmentKind.Diagnostic;
    public string Status { get; set; } = Constants.CandidateAssessmentStatus.PendingGeneration;

    public Guid? PersonalSetJobId { get; set; }
    public Guid? QuestionSetId { get; set; }
    public Guid? PracticeSessionId { get; set; }
    public Guid? PreviousAssessmentId { get; set; }

    public double? OverallReadiness { get; set; }
    public string? ReadinessStatus { get; set; }

    /// <summary>Snapshot context lúc Start Diagnostic (JSON).</summary>
    public string ContextSnapshotJson { get; set; } = "{}";
    /// <summary>
    /// SCRUM-454: JSON mảng skill mà lần đo này bao phủ.
    /// null/rỗng = full framework (diagnostic); có giá trị = partial (re-assessment 1 skill).
    /// Merge vào profile chỉ được ghi các skill trong phạm vi này.
    /// </summary>
    public string? ScopeSkillsJson { get; set; }
    /// <summary>Giải thích LLM (optional) — điểm không lấy từ đây.</summary>
    public string? ExplanationJson { get; set; }

    /// <summary>SCRUM-457: FRAMEWORK | ADAPTIVE | UNSUPPORTED.</summary>
    public string ResolutionMode { get; set; } = Constants.CompetencyResolutionMode.Framework;
    public string? RoleFamilyKey { get; set; }
    /// <summary>Snapshot CompetencyBlueprint (schemaVersion trong JSON) — source of truth cho scoring Adaptive.</summary>
    public string? BlueprintJson { get; set; }
    public int BlueprintSchemaVersion { get; set; } = Constants.CompetencyBlueprintSchema.CurrentVersion;

    public CompetencyFramework? Framework { get; set; }
    public CandidatePersonalSetJob? PersonalSetJob { get; set; }
    public QuestionSet? QuestionSet { get; set; }
    public PracticeSession? PracticeSession { get; set; }
    public CandidateAssessment? PreviousAssessment { get; set; }
    public ICollection<CandidateAssessmentSkillResult> SkillResults { get; set; } = new List<CandidateAssessmentSkillResult>();
}

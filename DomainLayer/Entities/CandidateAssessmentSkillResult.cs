namespace DomainLayer.Entities;

public class CandidateAssessmentSkillResult : BaseEntity
{
    public Guid AssessmentId { get; set; }
    public string Skill { get; set; } = string.Empty;
    public double SkillScore { get; set; }
    public double TargetScore { get; set; }
    public double Gap { get; set; }
    public double ImportanceWeight { get; set; }
    /// <summary>easy | medium | hard — mức khó cao nhất đã có evidence Succeeded.</summary>
    public string? DemonstratedDifficulty { get; set; }
    public string EvidenceJson { get; set; } = "[]";

    public CandidateAssessment Assessment { get; set; } = null!;
}

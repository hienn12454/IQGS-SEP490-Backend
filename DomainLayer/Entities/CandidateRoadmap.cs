namespace DomainLayer.Entities;

public class CandidateRoadmap : BaseEntity
{
    public Guid CandidateUserId { get; set; }
    public Guid? SourceAssessmentId { get; set; }
    public Guid? FrameworkId { get; set; }
    public string Skill { get; set; } = string.Empty;
    public double? CurrentScore { get; set; }
    public double TargetScore { get; set; }
    public double Gap { get; set; }
    /// <summary>SCRUM-455: Gap × ImportanceWeight — lưu để FE/test kiểm chứng được, không chỉ dùng lúc sort.</summary>
    public double PriorityScore { get; set; }
    /// <summary>gap | advanced — skill đã đạt target vẫn luyện được (advanced), không bị khóa.</summary>
    public string Kind { get; set; } = Constants.CandidateRoadmapKind.Gap;
    public string Priority { get; set; } = "medium";
    public string Status { get; set; } = Constants.CandidateRoadmapStatus.Suggested;
    public string? ExplanationJson { get; set; }
    /// <summary>SCRUM-457: framework | rag_dynamic.</summary>
    public string SourceMode { get; set; } = Constants.CompetencySourceMode.Framework;

    public CandidateAssessment? SourceAssessment { get; set; }
    public CompetencyFramework? Framework { get; set; }
    public ICollection<CandidateRoadmapItem> Items { get; set; } = new List<CandidateRoadmapItem>();
}

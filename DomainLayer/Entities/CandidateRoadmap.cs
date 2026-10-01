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
    /// <summary>SCRUM-462: null = chưa Accept preview; có giá trị = candidate đã chấp nhận lộ trình.</summary>
    public DateTime? AcceptedAt { get; set; }
    /// <summary>SCRUM-484: thứ tự hiển thị/luyện skill trong preview (0 = học trước). Khác PriorityScore (điểm ưu tiên AI).</summary>
    public int DisplayOrder { get; set; }

    public CandidateAssessment? SourceAssessment { get; set; }
    public CompetencyFramework? Framework { get; set; }
    public ICollection<CandidateRoadmapItem> Items { get; set; } = new List<CandidateRoadmapItem>();
}

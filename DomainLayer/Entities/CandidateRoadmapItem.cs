namespace DomainLayer.Entities;

public class CandidateRoadmapItem : BaseEntity
{
    public Guid RoadmapId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string? Subtopic { get; set; }
    public int SortOrder { get; set; }
    public string Status { get; set; } = Constants.CandidateRoadmapItemStatus.Pending;
    public bool IsReassessmentGate { get; set; }
    /// <summary>SCRUM-462: candidate chọn học topic này trong preview; gate luôn true.</summary>
    public bool IsIncluded { get; set; } = true;
    public Guid? DrillSessionId { get; set; }
    public Guid? DrillQuestionSetId { get; set; }
    public double? DrillScore { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceTitle { get; set; }
    /// <summary>Node curated đã dùng để sinh item này — truy vết nguồn.</summary>
    public Guid? RoadmapNodeId { get; set; }

    public CandidateRoadmap Roadmap { get; set; } = null!;
    public RoadmapNode? RoadmapNode { get; set; }
    public PracticeSession? DrillSession { get; set; }
    public QuestionSet? DrillQuestionSet { get; set; }
}

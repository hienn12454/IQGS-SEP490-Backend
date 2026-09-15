namespace DomainLayer.Entities;

public class CompetencyFrameworkSkill : BaseEntity
{
    public Guid FrameworkId { get; set; }
    public string Skill { get; set; } = string.Empty;
    /// <summary>Trọng số importance trong OverallReadiness (tổng framework ≈ 1.0).</summary>
    public double ImportanceWeight { get; set; }
    public double TargetScore { get; set; }
    /// <summary>easy | medium | hard — mức khó tối thiểu cần evidence.</summary>
    public string RequiredDifficulty { get; set; } = Constants.QuestionDifficultyLevel.Medium;
    /// <summary>JSON mảng topic/subskill gợi ý.</summary>
    public string TopicsJson { get; set; } = "[]";
    public int SortOrder { get; set; }

    public CompetencyFramework Framework { get; set; } = null!;
}

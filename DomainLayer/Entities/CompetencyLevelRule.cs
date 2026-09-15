namespace DomainLayer.Entities;

/// <summary>
/// SCRUM-454: luật xác định level đạt được. Đây là CONFIG TOÀN CỤC —
/// KHÔNG có FrameworkId/RoleKey, nên .NET/Java/Python/React dùng chung một bộ luật.
/// Framework chỉ cung cấp target score + required difficulty của từng skill.
/// </summary>
public class CompetencyLevelRule : BaseEntity
{
    /// <summary>Fresher | Junior | Middle | Senior.</summary>
    public string Level { get; set; } = string.Empty;
    /// <summary>Overall readiness tối thiểu.</summary>
    public double OverallThreshold { get; set; }
    /// <summary>Tỉ lệ skill đạt TargetScore tối thiểu (0..1).</summary>
    public double TargetMetRatio { get; set; }
    /// <summary>Tỉ lệ skill có DemonstratedDifficulty >= RequiredDifficulty (0..1).</summary>
    public double RequiredDifficultyRatio { get; set; }
    /// <summary>Tỉ lệ skill có evidence ở mức hard (0..1) — chặn "Middle/Senior" chỉ nhờ câu dễ.</summary>
    public double HardEvidenceRatio { get; set; }
    /// <summary>Thứ tự level từ thấp đến cao.</summary>
    public int SortOrder { get; set; }
}

namespace DomainLayer.Entities;

/// <summary>Singleton config công thức chấm Coach — không hardcode rải source.</summary>
public class CompetencyScoringPolicy : BaseEntity
{
    public static readonly Guid SingletonId = Guid.Parse("a1000000-0000-0000-0000-000000000001");

    public double CorrectnessWeight { get; set; } = 0.5;
    public double RelevanceWeight { get; set; } = 0.3;
    public double ClarityWeight { get; set; } = 0.2;

    public double EasyDifficultyWeight { get; set; } = 1.0;
    public double MediumDifficultyWeight { get; set; } = 1.5;
    public double HardDifficultyWeight { get; set; } = 2.0;

    public double DevelopingMaxExclusive { get; set; } = 50;
    public double NearTargetMaxExclusive { get; set; } = 70;
    public double ReadyMaxExclusive { get; set; } = 85;
    // Strong: >= ReadyMaxExclusive

    /// <summary>% core skills đạt target để Junior Ready.</summary>
    public double JuniorReadyCoreSkillRatio { get; set; } = 0.7;
    public double OverallReadyThreshold { get; set; } = 70;

    /// <summary>
    /// SCRUM-457: JSON {"Fresher":60,"Junior":70,...}. Null/thiếu key → fallback OverallReadyThreshold.
    /// Adaptive TargetScore đi qua CompetencyTargetScorePolicy.Resolve — không đổi scoring engine.
    /// </summary>
    public string? TargetScoreByLevelJson { get; set; }
}

namespace ApplicationLayer.DTOs.Coach;

/// <summary>
/// SCRUM-453: scoring policy là config vận hành toàn cục — không gắn RoleKey.
/// Admin tinh chỉnh ngưỡng; engine scoring không đổi theo stack.
/// </summary>
public class CompetencyScoringPolicyDto
{
    public double CorrectnessWeight { get; set; }
    public double RelevanceWeight { get; set; }
    public double ClarityWeight { get; set; }
    public double EasyDifficultyWeight { get; set; }
    public double MediumDifficultyWeight { get; set; }
    public double HardDifficultyWeight { get; set; }
    public double DevelopingMaxExclusive { get; set; }
    public double NearTargetMaxExclusive { get; set; }
    public double ReadyMaxExclusive { get; set; }
    public double JuniorReadyCoreSkillRatio { get; set; }
    public double OverallReadyThreshold { get; set; }
    /// <summary>SCRUM-457: optional {"Fresher":60,"Junior":70,...}. Null → OverallReadyThreshold.</summary>
    public string? TargetScoreByLevelJson { get; set; }
}

public class CompetencyLevelRuleDto
{
    public Guid? Id { get; set; }
    public string Level { get; set; } = string.Empty;
    public double OverallThreshold { get; set; }
    public double TargetMetRatio { get; set; }
    public double RequiredDifficultyRatio { get; set; }
    public double HardEvidenceRatio { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateCompetencyLevelRulesDto
{
    public List<CompetencyLevelRuleDto> Rules { get; set; } = new();
}

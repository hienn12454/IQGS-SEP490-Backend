using ApplicationLayer.DTOs.Candidate;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-457 AC #11: readiness toward TargetLevel — không tuyên bố seniority trên UI.
/// </summary>
public static class TargetReadinessMapper
{
    public static void Apply(
        CoachAssessmentDto dto,
        CompetencyScoringPolicy policy,
        string? targetLevel,
        string? estimatedBand)
    {
        var threshold = CompetencyTargetScorePolicy.Resolve(policy, targetLevel);
        var overall = dto.OverallReadiness ?? 0;
        dto.TargetLevel = string.IsNullOrWhiteSpace(targetLevel) ? CoachSeniorityLevel.Junior : targetLevel.Trim();
        dto.TargetThreshold = threshold;
        dto.ReadinessPercent = threshold <= 0
            ? 0
            : Math.Min(100, Math.Round(overall / threshold * 100, 2));
        dto.EstimatedBand = estimatedBand;
        dto.TargetReadinessStatus = overall >= threshold
            ? CompetencyTargetReadinessStatus.Ready
            : CompetencyTargetReadinessStatus.NotReady;
        dto.SkillGaps = dto.Skills
            .Where(s => s.Gap > 0)
            .Select(s => new CoachSkillGapDto
            {
                Skill = s.Skill,
                CurrentScore = s.SkillScore,
                TargetScore = s.TargetScore,
                Gap = s.Gap,
                PriorityScore = RoadmapRecommendationService.ComputePriorityScore(s.Gap, s.ImportanceWeight)
            })
            .OrderByDescending(g => g.PriorityScore)
            .ToList();
    }
}

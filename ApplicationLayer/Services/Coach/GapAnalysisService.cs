namespace ApplicationLayer.Services.Coach;

public sealed record SkillGap(
    string Skill,
    double CurrentScore,
    double TargetScore,
    double Gap,
    double Weight,
    double PriorityScore);

/// <summary>SCRUM-457: gap = targetScore − currentScore; priority = max(gap,0) × weight.</summary>
public static class GapAnalysisService
{
    public static List<SkillGap> Analyze(
        IEnumerable<(string Skill, double CurrentScore, double TargetScore, double Weight)> skills)
    {
        return skills
            .Select(s =>
            {
                var gap = Math.Round(s.TargetScore - s.CurrentScore, 2);
                return new SkillGap(
                    s.Skill,
                    s.CurrentScore,
                    s.TargetScore,
                    gap,
                    s.Weight,
                    RoadmapRecommendationService.ComputePriorityScore(gap, s.Weight));
            })
            .OrderByDescending(g => g.PriorityScore)
            .ThenByDescending(g => g.Gap)
            .ToList();
    }
}

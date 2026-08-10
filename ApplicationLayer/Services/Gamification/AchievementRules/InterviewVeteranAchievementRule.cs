using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;

namespace ApplicationLayer.Services.Gamification.AchievementRules;

/// <summary>Hoàn thành 100 phiên luyện tập.</summary>
public class InterviewVeteranAchievementRule : IAchievementRule
{
    private const int TargetCount = 100;

    public string Code => AchievementCode.InterviewVeteran;

    public bool IsUnlocked(AchievementEvaluationContext context)
        => context.TotalSessionsCompleted >= TargetCount;
}

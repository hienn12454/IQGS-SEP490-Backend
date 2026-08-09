using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;

namespace ApplicationLayer.Services.Gamification.AchievementRules;

/// <summary>Hoàn thành 100 câu hỏi.</summary>
public class DedicatedAchievementRule : IAchievementRule
{
    private const int TargetCount = 100;

    public string Code => AchievementCode.Dedicated;

    public bool IsUnlocked(AchievementEvaluationContext context)
        => context.TotalQuestionsCompleted >= TargetCount;
}

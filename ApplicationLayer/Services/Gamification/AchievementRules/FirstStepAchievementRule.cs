using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;

namespace ApplicationLayer.Services.Gamification.AchievementRules;

/// <summary>Hoàn thành phiên luyện tập đầu tiên.</summary>
public class FirstStepAchievementRule : IAchievementRule
{
    public string Code => AchievementCode.FirstStep;

    public bool IsUnlocked(AchievementEvaluationContext context)
        => context.TotalSessionsCompleted >= 1;
}

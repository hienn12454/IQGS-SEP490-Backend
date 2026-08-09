using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;

namespace ApplicationLayer.Services.Gamification.AchievementRules;

/// <summary>Hoàn thành 50 câu hỏi Technical.</summary>
public class TechnicalMindAchievementRule : IAchievementRule
{
    private const int TargetCount = 50;

    public string Code => AchievementCode.TechnicalMind;

    public bool IsUnlocked(AchievementEvaluationContext context)
        => context.TotalTechnicalQuestionsCompleted >= TargetCount;
}

using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;

namespace ApplicationLayer.Services.Gamification.AchievementRules;

/// <summary>Hoàn thành 30 câu hỏi System Design.</summary>
public class SystemThinkerAchievementRule : IAchievementRule
{
    private const int TargetCount = 30;

    public string Code => AchievementCode.SystemThinker;

    public bool IsUnlocked(AchievementEvaluationContext context)
        => context.TotalSystemDesignQuestionsCompleted >= TargetCount;
}

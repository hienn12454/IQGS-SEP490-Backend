using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;

namespace ApplicationLayer.Services.Gamification.AchievementRules;

/// <summary>Hoàn thành daily goal ở 10 ngày (local date) khác nhau.</summary>
public class ConsistencyAchievementRule : IAchievementRule
{
    private const int TargetDays = 10;

    public string Code => AchievementCode.Consistency;

    public bool IsUnlocked(AchievementEvaluationContext context)
        => context.DailyGoalCompletedDaysCount >= TargetDays;
}

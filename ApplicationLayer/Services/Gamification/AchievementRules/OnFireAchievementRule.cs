using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;

namespace ApplicationLayer.Services.Gamification.AchievementRules;

/// <summary>Đạt streak 7 ngày.</summary>
public class OnFireAchievementRule : IAchievementRule
{
    private const int TargetStreakDays = 7;

    public string Code => AchievementCode.OnFire;

    public bool IsUnlocked(AchievementEvaluationContext context)
        => context.CurrentStreak >= TargetStreakDays;
}

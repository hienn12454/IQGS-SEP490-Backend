using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;

namespace ApplicationLayer.Services.Gamification.AchievementRules;

/// <summary>Đạt điểm &gt;= 90 ở 1 câu hỏi đã hoàn thành.</summary>
public class ExcellentAnswerAchievementRule : IAchievementRule
{
    private const double TargetScore = 90;

    public string Code => AchievementCode.ExcellentAnswer;

    public bool IsUnlocked(AchievementEvaluationContext context)
        => context.LastQuestionScore is >= TargetScore;
}

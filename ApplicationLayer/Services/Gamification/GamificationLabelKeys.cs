using DomainLayer.Constants;

namespace ApplicationLayer.Services.Gamification;

/// <summary>i18n key cho từng loại XP transaction — FE tự dịch, backend không trả text tiếng Việt cứng.</summary>
public static class GamificationLabelKeys
{
    public static string ForType(string xpTransactionType) => xpTransactionType switch
    {
        XpTransactionType.QuestionCompleted => "gamification.questionCompleted",
        XpTransactionType.ScoreBonus => "gamification.scoreBonus",
        XpTransactionType.QuestionSetCompleted => "gamification.questionSetCompleted",
        XpTransactionType.ImprovementBonus => "gamification.improvementBonus",
        XpTransactionType.StreakMilestone => "gamification.streakMilestone",
        XpTransactionType.Achievement => "gamification.achievementUnlocked",
        XpTransactionType.Adjustment => "gamification.adjustment",
        _ => "gamification.unknown"
    };
}

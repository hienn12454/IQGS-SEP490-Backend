namespace DomainLayer.Constants;

/// <summary>Loại giao dịch XP — chỉ để phân loại/audit, KHÔNG chứa business logic (xem GamificationOptions/IXpRewardPolicy).</summary>
public static class XpTransactionType
{
    public const string QuestionCompleted = "QuestionCompleted";
    public const string ScoreBonus = "ScoreBonus";
    public const string QuestionSetCompleted = "QuestionSetCompleted";
    public const string ImprovementBonus = "ImprovementBonus";
    public const string StreakMilestone = "StreakMilestone";
    public const string Achievement = "Achievement";
    public const string Adjustment = "Adjustment";
}

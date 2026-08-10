namespace ApplicationLayer.Interfaces.Services;

/// <summary>
/// Snapshot trạng thái gamification của 1 user tại thời điểm đánh giá achievement — đọc từ UserProgress
/// (đã update trong cùng transaction) + sự kiện vừa xảy ra. Pure data, không truy vấn DB.
/// </summary>
public sealed class AchievementEvaluationContext
{
    public required Guid UserId { get; init; }
    public required int CurrentStreak { get; init; }
    public required int TotalQuestionsCompleted { get; init; }
    public required int TotalSessionsCompleted { get; init; }
    public required int TotalTechnicalQuestionsCompleted { get; init; }
    public required int TotalSystemDesignQuestionsCompleted { get; init; }
    public required int DailyGoalCompletedDaysCount { get; init; }

    /// <summary>Điểm AI chấm của câu hỏi vừa hoàn thành trong sự kiện này — null nếu sự kiện không phải QuestionCompleted.</summary>
    public double? LastQuestionScore { get; init; }
}

/// <summary>
/// 1 rule = 1 achievement — strategy pattern, tránh if/else achievement dồn trong 1 chỗ.
/// Rule chỉ đọc context (không I/O); GamificationService chịu trách nhiệm build context, lọc rule đã
/// unlock, và ghi UserAchievement idempotent.
/// </summary>
public interface IAchievementRule
{
    /// <summary>Mã achievement — khớp DomainLayer.Constants.AchievementCode.</summary>
    string Code { get; }

    bool IsUnlocked(AchievementEvaluationContext context);
}

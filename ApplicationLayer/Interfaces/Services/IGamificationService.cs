using ApplicationLayer.DTOs.Gamification;

namespace ApplicationLayer.Interfaces.Services;

/// <summary>
/// Orchestrator trung tâm của hệ Gamification — điểm duy nhất được phép ghi UserProgress/XpTransaction/
/// DailyProgress/UserAchievement. Implementation (InfrastructureLayer.Services.Gamification.GamificationService)
/// đảm nhiệm transaction + row-lock cho atomicity/concurrency (xem class đó để biết chi tiết).
/// Không có API public nào cho phép client tự chọn amount XP — 2 method Award* chỉ được gọi từ
/// CandidatePracticeSessionService, dựa trên trạng thái đã persist ở backend.
/// </summary>
public interface IGamificationService
{
    /// <summary>
    /// Award QuestionCompleted + ScoreBonus + (ImprovementBonus nếu đủ điều kiện) + xử lý streak.
    /// Idempotent theo CandidateAnswerId — gọi lại nhiều lần cho cùng answer chỉ award 1 lần.
    /// Trả null nếu toàn bộ event đã được award trước đó (không có gì mới để trả về FE).
    /// </summary>
    Task<XpRewardDto?> AwardQuestionCompletionAsync(QuestionCompletionXpContext context);

    /// <summary>Award QuestionSetCompleted — idempotent theo PracticeSessionId.</summary>
    Task<XpRewardDto?> AwardQuestionSetCompletionAsync(QuestionSetCompletionXpContext context);

    Task<UserProgressDto> GetProgressAsync(Guid userId);

    /// <summary>Activity heatmap trong khoảng [from, to] (inclusive) — range bị giới hạn bởi GamificationOptions.MaxActivityRangeDays.</summary>
    Task<IReadOnlyList<DailyActivityDto>> GetActivityAsync(Guid userId, DateOnly from, DateOnly to);

    Task<PagedResultDto<XpHistoryItemDto>> GetXpHistoryAsync(Guid userId, int page, int pageSize);

    Task<IReadOnlyList<AchievementDto>> GetAchievementsAsync(Guid userId);

    /// <summary>Cập nhật mục tiêu XP/ngày — giá trị đã được validate ở FluentValidation trước khi vào đây.</summary>
    Task<UserProgressDto> UpdateDailyGoalAsync(Guid userId, int dailyGoalXp);
}

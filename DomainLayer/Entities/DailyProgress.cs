namespace DomainLayer.Entities;

/// <summary>
/// Hoạt động gamification của 1 Candidate trong 1 ngày (theo LocalDate từ IUserLocalDateProvider) —
/// dùng cho activity heatmap + daily goal. Unique (UserId, LocalDate).
/// </summary>
public class DailyProgress : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateOnly LocalDate { get; set; }

    public int XpEarned { get; set; }
    public int QuestionsCompleted { get; set; }
    public int SessionsCompleted { get; set; }

    /// <summary>
    /// True kể từ lần đầu XpEarned trong ngày này >= DailyGoalXp tại thời điểm đó — là sự kiện lịch sử,
    /// KHÔNG bị gỡ lại nếu DailyGoalXp đổi sau đó.
    /// </summary>
    public bool DailyGoalCompleted { get; set; }
}

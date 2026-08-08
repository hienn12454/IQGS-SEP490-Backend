namespace DomainLayer.Entities;

/// <summary>
/// Trạng thái gamification tổng hợp của 1 Candidate — 1-1 với User. Đây là bản snapshot hiện tại
/// (đọc nhanh cho GET progress); lịch sử chi tiết nằm ở XpTransaction/DailyProgress.
/// TotalXp/Level/CurrentStreak/LongestStreak/các counter Total* CHỈ được ghi bởi GamificationService
/// bên trong transaction có row-lock — không service nào khác được sửa trực tiếp các field này.
/// </summary>
public class UserProgress : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public long TotalXp { get; set; }
    public int Level { get; set; } = 1;

    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }

    /// <summary>LocalDate (theo IUserLocalDateProvider) gần nhất có hoạt động luyện tập hợp lệ tính streak.</summary>
    public DateOnly? LastActiveLocalDate { get; set; }

    /// <summary>
    /// Mục tiêu XP/ngày do candidate chọn — chỉ dùng cho UI progress, không tự sinh thêm XP.
    /// Default = 50, GamificationService luôn set tường minh giá trị từ GamificationOptions.DefaultDailyGoalXp
    /// khi khởi tạo record mới — literal ở đây chỉ là fallback an toàn cho constructor trần.
    /// </summary>
    public int DailyGoalXp { get; set; } = 50;

    // ── Denormalized counters — cập nhật atomically cùng TotalXp, phục vụ đánh giá achievement
    //    không cần quét COUNT toàn bảng ở mỗi lần award XP. ──────────────────────────────────
    public int TotalQuestionsCompleted { get; set; }
    public int TotalSessionsCompleted { get; set; }
    public int TotalTechnicalQuestionsCompleted { get; set; }
    public int TotalSystemDesignQuestionsCompleted { get; set; }

    /// <summary>Số ngày (local date) khác nhau đã đạt DailyGoalXp — dùng cho achievement CONSISTENCY.</summary>
    public int DailyGoalCompletedDaysCount { get; set; }
}

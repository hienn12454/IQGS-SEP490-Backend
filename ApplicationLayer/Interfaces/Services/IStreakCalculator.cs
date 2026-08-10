namespace ApplicationLayer.Interfaces.Services;

public readonly record struct StreakResult(int CurrentStreak, int LongestStreak, bool IsNewActiveDay);

/// <summary>
/// Tính lại streak khi candidate có 1 hoạt động luyện tập hợp lệ (câu hỏi hoàn thành) trong ngày `today`
/// (LocalDate theo IUserLocalDateProvider). Pure — không I/O, không phụ thuộc DateTime.Now.
/// </summary>
public interface IStreakCalculator
{
    /// <summary>
    /// - previousActiveLocalDate == today - 1 ngày → currentStreak + 1.
    /// - previousActiveLocalDate == today → giữ nguyên (đã tính hôm nay rồi, IsNewActiveDay = false).
    /// - previousActiveLocalDate &lt; today - 1 ngày (hoặc null) → currentStreak = 1 (streak mới/đứt quãng).
    /// LongestStreak = max(LongestStreak cũ, CurrentStreak mới).
    /// </summary>
    StreakResult Compute(
        DateOnly? previousActiveLocalDate,
        int previousCurrentStreak,
        int previousLongestStreak,
        DateOnly today);
}

using ApplicationLayer.Interfaces.Services;

namespace ApplicationLayer.Services.Gamification;

public class StreakCalculator : IStreakCalculator
{
    public StreakResult Compute(
        DateOnly? previousActiveLocalDate,
        int previousCurrentStreak,
        int previousLongestStreak,
        DateOnly today)
    {
        if (previousActiveLocalDate == today)
        {
            // Đã có hoạt động tính streak hôm nay rồi — câu hỏi thứ 2+ trong ngày không đổi streak.
            var longestUnchanged = Math.Max(previousLongestStreak, previousCurrentStreak);
            return new StreakResult(previousCurrentStreak, longestUnchanged, IsNewActiveDay: false);
        }

        var newStreak = previousActiveLocalDate == today.AddDays(-1)
            ? previousCurrentStreak + 1  // liên tiếp ngày hôm qua → nối dài streak
            : 1;                          // đứt quãng (hoặc lần đầu tiên) → streak mới bắt đầu từ 1

        var newLongest = Math.Max(previousLongestStreak, newStreak);
        return new StreakResult(newStreak, newLongest, IsNewActiveDay: true);
    }
}

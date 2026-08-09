namespace ApplicationLayer.Interfaces.Services;

/// <summary>
/// Quy đổi 1 thời điểm UTC → LocalDate của user — cô lập toàn bộ business logic streak/daily-progress
/// khỏi việc User hiện chưa lưu timezone. Fallback hiện tại: UTC cho mọi user.
/// Khi hệ thống lưu timezone thật (vd IANA id trên User/CandidateProfile), chỉ cần đổi implementation
/// này — không đụng vào GamificationService/StreakCalculator/DailyProgress.
/// </summary>
public interface IUserLocalDateProvider
{
    Task<DateOnly> GetLocalDateAsync(Guid userId, DateTime utcInstant);
}

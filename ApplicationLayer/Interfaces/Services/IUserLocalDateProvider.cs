namespace ApplicationLayer.Interfaces.Services;

/// <summary>
/// Quy đổi 1 thời điểm UTC → LocalDate của user — cô lập toàn bộ business logic streak/daily-progress
/// khỏi nguồn timezone thật sự. Implementation (UserLocalDateProvider) đọc CandidateProfile.TimeZoneId
/// (IANA id, đặt qua PATCH /api/users/me/candidate-profile); user chưa chọn timezone → fallback UTC.
/// </summary>
public interface IUserLocalDateProvider
{
    Task<DateOnly> GetLocalDateAsync(Guid userId, DateTime utcInstant);
}

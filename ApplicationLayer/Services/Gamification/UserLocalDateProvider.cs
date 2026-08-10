using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services.Gamification;

/// <summary>
/// Quy đổi UTC → LocalDate theo CandidateProfile.TimeZoneId (IANA id, đặt qua
/// PATCH /api/users/me/candidate-profile). Chưa chọn timezone (null) hoặc user không phải Candidate
/// (không có CandidateProfile) → fallback UTC. TimeZoneId lưu trong DB nhưng không còn resolve được
/// (vd tzdata đổi giữa các lần deploy) → cũng fallback UTC, không throw — business logic streak/daily
/// progress không được phép fail vì lỗi tra timezone.
/// </summary>
public class UserLocalDateProvider : IUserLocalDateProvider
{
    private readonly ICandidateProfileRepository _candidateProfileRepo;
    private readonly ILogger<UserLocalDateProvider> _logger;

    public UserLocalDateProvider(
        ICandidateProfileRepository candidateProfileRepo, ILogger<UserLocalDateProvider> logger)
    {
        _candidateProfileRepo = candidateProfileRepo;
        _logger = logger;
    }

    public async Task<DateOnly> GetLocalDateAsync(Guid userId, DateTime utcInstant)
    {
        var utc = utcInstant.Kind == DateTimeKind.Utc
            ? utcInstant
            : DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);

        var timeZoneId = (await _candidateProfileRepo.GetByUserIdAsync(userId))?.TimeZoneId;
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return DateOnly.FromDateTime(utc);

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            return DateOnly.FromDateTime(local);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _logger.LogWarning(ex,
                "TimeZoneId '{TimeZoneId}' của user {UserId} không resolve được — fallback UTC.",
                timeZoneId, userId);
            return DateOnly.FromDateTime(utc);
        }
    }
}

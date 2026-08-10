using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Services.Gamification;
using DomainLayer.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApplicationLayer.UnitTests.Gamification;

public class UserLocalDateProviderTests
{
    /// <summary>UTC 2026-08-09 17:30 = 2026-08-10 00:30 giờ Việt Nam (UTC+7) — cố tình chọn thời điểm lệch ngày qua biên UTC.</summary>
    private static readonly DateTime UtcInstant = new(2026, 8, 9, 17, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task NoCandidateProfile_FallsBackToUtcDate()
    {
        var provider = new UserLocalDateProvider(new FakeCandidateProfileRepository(null), NullLogger<UserLocalDateProvider>.Instance);

        var result = await provider.GetLocalDateAsync(Guid.NewGuid(), UtcInstant);

        Assert.Equal(new DateOnly(2026, 8, 9), result);
    }

    [Fact]
    public async Task TimeZoneIdNotSet_FallsBackToUtcDate()
    {
        var profile = new CandidateProfile { TimeZoneId = null };
        var provider = new UserLocalDateProvider(new FakeCandidateProfileRepository(profile), NullLogger<UserLocalDateProvider>.Instance);

        var result = await provider.GetLocalDateAsync(Guid.NewGuid(), UtcInstant);

        Assert.Equal(new DateOnly(2026, 8, 9), result);
    }

    [Fact]
    public async Task ValidTimeZoneId_ConvertsAcrossDateBoundary()
    {
        var profile = new CandidateProfile { TimeZoneId = "Asia/Ho_Chi_Minh" };
        var provider = new UserLocalDateProvider(new FakeCandidateProfileRepository(profile), NullLogger<UserLocalDateProvider>.Instance);

        var result = await provider.GetLocalDateAsync(Guid.NewGuid(), UtcInstant);

        // UTC+7 đẩy 17:30 UTC (09/08) sang 00:30 giờ VN (10/08) — khác ngày với UTC.
        Assert.Equal(new DateOnly(2026, 8, 10), result);
    }

    [Fact]
    public async Task InvalidTimeZoneId_FallsBackToUtcDate_DoesNotThrow()
    {
        // Dữ liệu hỏng giả định (vd tzdata đổi giữa các lần deploy) — không được throw, phải fallback UTC.
        var profile = new CandidateProfile { TimeZoneId = "Not/A_Real_Zone" };
        var provider = new UserLocalDateProvider(new FakeCandidateProfileRepository(profile), NullLogger<UserLocalDateProvider>.Instance);

        var result = await provider.GetLocalDateAsync(Guid.NewGuid(), UtcInstant);

        Assert.Equal(new DateOnly(2026, 8, 9), result);
    }

    private sealed class FakeCandidateProfileRepository : ICandidateProfileRepository
    {
        private readonly CandidateProfile? _profile;

        public FakeCandidateProfileRepository(CandidateProfile? profile) => _profile = profile;

        public Task<CandidateProfile?> GetByUserIdAsync(Guid userId) => Task.FromResult(_profile);

        public Task<CandidateProfile?> GetByIdAsync(Guid id) => throw new NotImplementedException();
        public Task<IEnumerable<CandidateProfile>> GetAllAsync() => throw new NotImplementedException();
        public Task AddAsync(CandidateProfile entity) => throw new NotImplementedException();
        public Task UpdateAsync(CandidateProfile entity) => throw new NotImplementedException();
        public Task DeleteAsync(CandidateProfile entity) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(Guid id) => throw new NotImplementedException();
    }
}

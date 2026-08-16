using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.Hr;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

/// <summary>SCRUM-398 + SCRUM-401: HR xem overview candidate — profile + stats + contribution heatmap.</summary>
public class HrCandidateOverviewService : IHrCandidateOverviewService
{
    private const int FastSessionThresholdMinutes = 35;
    /// <summary>Cửa sổ heatmap giống GitHub (~1 năm).</summary>
    private const int HeatmapWeeks = 52;

    private readonly IUserRepository _userRepository;
    private readonly ICandidateProfileRepository _profileRepository;
    private readonly IPracticeSessionRepository _practiceSessionRepository;

    public HrCandidateOverviewService(
        IUserRepository userRepository,
        ICandidateProfileRepository profileRepository,
        IPracticeSessionRepository practiceSessionRepository)
    {
        _userRepository = userRepository;
        _profileRepository = profileRepository;
        _practiceSessionRepository = practiceSessionRepository;
    }

    public async Task<HrCandidateOverviewDto> GetOverviewAsync(Guid candidateUserId, Guid hrUserId)
    {
        var profile = await _profileRepository.GetByUserIdAsync(candidateUserId);
        if (profile is null)
            throw new NotFoundException("Không tìm thấy hồ sơ ứng viên.");

        if (!profile.AllowRecruiterRecommendation)
            throw new ForbiddenException("Ứng viên không cho phép HR xem hồ sơ.");

        var hasEngagement = await _practiceSessionRepository.HasAnySessionOnHrOwnedSetsAsync(candidateUserId, hrUserId);
        if (!hasEngagement)
            throw new ForbiddenException("Bạn chỉ xem được ứng viên đã luyện bộ câu hỏi của mình.");

        var user = await _userRepository.GetByIdAnyStatusAsync(candidateUserId)
            ?? throw new NotFoundException("Không tìm thấy ứng viên.");

        var stats = await _practiceSessionRepository.GetStatsAsync(candidateUserId, null, null);
        var (completedItems, _) = await _practiceSessionRepository.ListAsync(
            candidateUserId,
            status: PracticeSessionStatus.Completed,
            questionSetId: null,
            keyword: null,
            fromDate: null,
            toDate: null,
            page: 1,
            pageSize: 200);

        var streakDays = ComputeStreakDays(completedItems.Select(s => s.CompletedAt));
        var hasFastSession = HasFastCompletedSession(completedItems);

        var practiceOnMySets = await _practiceSessionRepository.ListSessionsOnHrOwnedSetsAsync(candidateUserId, hrUserId);

        // SCRUM-401: aggregate heatmap độc lập pageSize ListAsync (52 tuần theo ngày local).
        var fromLocal = DateTime.Now.Date.AddDays(-(HeatmapWeeks * 7 - 1));
        var fromUtc = DateTime.SpecifyKind(fromLocal, DateTimeKind.Local).ToUniversalTime();
        var heatmapRaw = await _practiceSessionRepository.ListCompletedTimestampsForHeatmapAsync(candidateUserId, fromUtc);
        var (heatmapDays, longestStreak, activeDays) = BuildHeatmapBuckets(heatmapRaw, HeatmapWeeks);

        return new HrCandidateOverviewDto
        {
            CandidateUserId = candidateUserId,
            CandidateName = user.FullName,
            CandidateEmail = user.Email,
            TargetRole = profile.TargetRole,
            SeniorityLevel = profile.SeniorityLevel,
            AvatarUrl = user.AvatarUrl,
            Bio = profile.Bio,
            TechStack = profile.TechStack?.ToList() ?? new List<string>(),
            LinkedInUrl = profile.LinkedInUrl,
            GithubUrl = profile.GithubUrl,
            TotalSessions = stats.TotalSessions,
            AverageScore = stats.AverageScore,
            BestScore = stats.BestScore,
            CurrentStreakDays = streakDays,
            HasFastSession = hasFastSession,
            HeatmapDays = heatmapDays,
            LongestStreakDays = longestStreak,
            ActiveDaysInWindow = activeDays,
            Achievements = BuildAchievements(stats.TotalSessions, stats.BestScore, streakDays, hasFastSession),
            PracticeOnMySets = practiceOnMySets.Select(r => new HrCandidatePracticeOnMySetItemDto
            {
                SessionId = r.SessionId,
                QuestionSetId = r.QuestionSetId,
                Title = r.Title,
                SessionStatus = r.SessionStatus,
                OverallScore = r.OverallScore,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt
            }).ToList()
        };
    }

    /// <summary>
    /// SCRUM-401: group theo ngày local — chỉ trả ngày có hoạt động; FE fill đủ 52 tuần.
    /// Longest streak tính trong cửa sổ heatmap (không phải toàn lịch sử).
    /// </summary>
    internal static (List<HrCandidateHeatmapDayDto> Days, int LongestStreak, int ActiveDays) BuildHeatmapBuckets(
        IReadOnlyList<PracticeSessionHeatmapRawRow> raw, int weeks)
    {
        var byDay = new Dictionary<string, HrCandidateHeatmapDayDto>(StringComparer.Ordinal);
        foreach (var row in raw)
        {
            var local = ToLocal(row.CompletedAt);
            var key = $"{local.Year}-{local.Month:D2}-{local.Day:D2}";
            if (!byDay.TryGetValue(key, out var bucket))
            {
                bucket = new HrCandidateHeatmapDayDto { Date = key, Count = 0, Minutes = 0 };
                byDay[key] = bucket;
            }

            bucket.Count += 1;
            var duration = row.CompletedAt - row.StartedAt;
            if (duration.TotalSeconds > 0)
                bucket.Minutes += (int)Math.Round(duration.TotalMinutes);
        }

        var totalDays = weeks * 7;
        var orderedKeys = new List<string>(totalDays);
        for (var i = totalDays - 1; i >= 0; i--)
        {
            var d = DateTime.Now.Date.AddDays(-i);
            orderedKeys.Add($"{d.Year}-{d.Month:D2}-{d.Day:D2}");
        }

        var longest = 0;
        var running = 0;
        foreach (var key in orderedKeys)
        {
            if (byDay.ContainsKey(key))
            {
                running++;
                if (running > longest) longest = running;
            }
            else
            {
                running = 0;
            }
        }

        return (byDay.Values.OrderBy(d => d.Date).ToList(), longest, byDay.Count);
    }

    private static DateTime ToLocal(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return value.ToLocalTime();
        return DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();
    }

    private static List<HrCandidateAchievementFlagDto> BuildAchievements(
        int totalSessions, double? bestScore, int streakDays, bool hasFastSession)
        => new()
        {
            new() { Id = "first-practice", Earned = totalSessions >= 1 },
            new() { Id = "streak-7", Earned = streakDays >= 7 },
            new() { Id = "high-scorer", Earned = bestScore is >= 90 },
            new() { Id = "speed-demon", Earned = hasFastSession },
            new() { Id = "consistent-learner", Earned = totalSessions >= 20 },
        };

    /// <summary>Streak theo ngày lịch local (giống FE computeStreakDays) — CompletedAt UTC convert local.</summary>
    internal static int ComputeStreakDays(IEnumerable<DateTime?> completedAtList)
    {
        var days = new HashSet<string>();
        foreach (var completedAt in completedAtList)
        {
            if (!completedAt.HasValue) continue;
            var local = completedAt.Value.Kind == DateTimeKind.Utc
                ? completedAt.Value.ToLocalTime()
                : DateTime.SpecifyKind(completedAt.Value, DateTimeKind.Utc).ToLocalTime();
            days.Add($"{local.Year}-{local.Month:D2}-{local.Day:D2}");
        }

        var streak = 0;
        var cursor = DateTime.Now.Date;
        if (!days.Contains($"{cursor.Year}-{cursor.Month:D2}-{cursor.Day:D2}"))
            cursor = cursor.AddDays(-1);

        while (days.Contains($"{cursor.Year}-{cursor.Month:D2}-{cursor.Day:D2}"))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    private static bool HasFastCompletedSession(IReadOnlyList<PracticeSessionRow> items)
    {
        var thresholdSeconds = FastSessionThresholdMinutes * 60;
        return items.Any(s =>
            s.StartedAt.HasValue
            && s.CompletedAt.HasValue
            && (s.CompletedAt.Value - s.StartedAt.Value).TotalSeconds > 0
            && (s.CompletedAt.Value - s.StartedAt.Value).TotalSeconds <= thresholdSeconds);
    }
}

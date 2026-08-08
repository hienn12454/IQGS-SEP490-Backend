using ApplicationLayer.DTOs.Hr;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Studio.Enums;

namespace ApplicationLayer.Services;

/// <summary>
/// Dashboard HR — KPI/activity từ Studio projects + InterviewQuestions (không dùng job V1).
/// </summary>
public class HrDashboardService : IHrDashboardService
{
    private const string SimplifiedCompleted = "COMPLETED";
    private const string SimplifiedFailed = "FAILED";
    private const string SimplifiedProcessing = "PROCESSING";

    private readonly IHrDashboardStudioStatsRepository _studioStats;
    private readonly ICandidateRecommendationRepository _recommendationRepository;
    private readonly ISubscriptionService _subscriptionService;

    public HrDashboardService(
        IHrDashboardStudioStatsRepository studioStats,
        ICandidateRecommendationRepository recommendationRepository,
        ISubscriptionService subscriptionService)
    {
        _studioStats = studioStats;
        _recommendationRepository = recommendationRepository;
        _subscriptionService = subscriptionService;
    }

    public async Task<HrDashboardResponseDto> GetDashboardAsync(Guid hrUserId, HrDashboardQueryDto query)
    {
        var activityDays = Math.Clamp(query.ActivityDays <= 0 ? 30 : query.ActivityDays, 1, 365);
        var recentLimit = Math.Clamp(query.RecentLimit <= 0 ? 7 : query.RecentLimit, 1, 50);
        var recommendationsLimit = Math.Clamp(query.RecommendationsLimit <= 0 ? 8 : query.RecommendationsLimit, 1, 50);

        var projects = await _studioStats.GetProjectStatsByOwnerAsync(hrUserId);

        var totalSessions = projects.Count;
        var completedSessions = projects.Count(IsCompleted);
        var totalQuestionsGenerated = projects.Sum(p => p.QuestionCount);
        var successRatePercent = totalSessions == 0
            ? 0
            : Math.Round((double)completedSessions / totalSessions * 100, 2);

        var nowUtc = DateTime.UtcNow;
        var thisMonthSessions = projects.Count(p =>
            p.CreatedAt.Year == nowUtc.Year && p.CreatedAt.Month == nowUtc.Month);

        var topRole = projects
            .Select(p => p.RoleTitle)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .GroupBy(r => r!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        var dailyActivity = BuildDailyActivity(projects, activityDays, nowUtc.Date);

        var questionTypeDistribution = projects
            .SelectMany(p => p.QuestionTypeCounts)
            .GroupBy(t => QuestionTypeNormalizer.NormalizeOne(t.Type), StringComparer.OrdinalIgnoreCase)
            .Select(g => new HrDashboardQuestionTypeDistributionItemDto
            {
                Type = g.Key,
                Count = g.Sum(x => x.Count)
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        var topQuestionType = questionTypeDistribution.FirstOrDefault()?.Type;

        var recentSessions = projects
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .Take(recentLimit)
            .Select(p => new HrDashboardRecentSessionDto
            {
                Id = p.ProjectId,
                RoleTitle = p.RoleTitle ?? p.Name,
                Status = MapSimplifiedStatus(p),
                QuestionCount = p.QuestionCount,
                CreatedAt = p.CreatedAt
            })
            .ToList();

        var thisWeekStart = nowUtc.Date.AddDays(-6);
        var lastWeekStart = thisWeekStart.AddDays(-7);
        var thisWeekCount = projects.Count(p => p.CreatedAt.Date >= thisWeekStart);
        var lastWeekCount = projects.Count(p =>
            p.CreatedAt.Date >= lastWeekStart && p.CreatedAt.Date < thisWeekStart);
        var weekOverWeekDelta = thisWeekCount - lastWeekCount;
        var weekOverWeekTrend = weekOverWeekDelta > 0 ? "up" : weekOverWeekDelta < 0 ? "down" : "flat";

        var (recommendationRows, _) = await _recommendationRepository.ListByHrAsync(
            hrUserId,
            page: 1,
            pageSize: recommendationsLimit,
            status: null,
            questionSetId: null,
            minScore: null,
            sortBy: "score",
            sortDir: "desc");

        var topRecommendations = recommendationRows
            .Select(r => new HrDashboardTopRecommendationDto
            {
                Id = r.Id,
                CandidateName = r.CandidateName,
                TargetRole = r.TargetRole,
                Score = r.OverallScore,
                Status = r.Status
            })
            .ToList();

        return new HrDashboardResponseDto
        {
            Kpis = new HrDashboardKpisDto
            {
                TotalSessions = totalSessions,
                CompletedSessions = completedSessions,
                TotalQuestionsGenerated = totalQuestionsGenerated,
                SuccessRatePercent = successRatePercent,
                ThisMonthSessions = thisMonthSessions,
                TopRole = topRole
            },
            DailyActivity = dailyActivity,
            QuestionTypeDistribution = questionTypeDistribution,
            RecentSessions = recentSessions,
            Insights = new HrDashboardInsightsDto
            {
                TopRole = topRole,
                SuccessRatePercent = successRatePercent,
                TopQuestionType = topQuestionType,
                WeekOverWeekTrend = weekOverWeekTrend,
                WeekOverWeekDelta = weekOverWeekDelta
            },
            TopRecommendations = topRecommendations,
            Subscription = await ResolveSubscriptionDtoAsync(hrUserId)
        };
    }

    private async Task<HrDashboardSubscriptionDto> ResolveSubscriptionDtoAsync(Guid hrUserId)
    {
        try
        {
            var sub = await _subscriptionService.GetMySubscriptionAsync(hrUserId);
            return new HrDashboardSubscriptionDto
            {
                PlanId = sub.PlanCode.Contains("PREMIUM", StringComparison.OrdinalIgnoreCase) ? "Premium" : "Free"
            };
        }
        catch
        {
            return new HrDashboardSubscriptionDto { PlanId = "Free" };
        }
    }

    private static List<HrDashboardDailyActivityItemDto> BuildDailyActivity(
        IReadOnlyList<HrStudioProjectStatRow> projects, int activityDays, DateTime todayUtc)
    {
        var fromDate = todayUtc.AddDays(-(activityDays - 1));
        var countsByDay = projects
            .GroupBy(p => p.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        return Enumerable.Range(0, activityDays)
            .Select(offset => fromDate.AddDays(offset))
            .Select(day => new HrDashboardDailyActivityItemDto
            {
                Date = day.ToString("yyyy-MM-dd"),
                Count = countsByDay.TryGetValue(day, out var count) ? count : 0
            })
            .ToList();
    }

    private static bool IsCompleted(HrStudioProjectStatRow p)
        => p.Status is InterviewProjectStatus.Generated
           || p.QuestionCount > 0;

    private static string MapSimplifiedStatus(HrStudioProjectStatRow p)
    {
        if (p.Status == InterviewProjectStatus.Archived)
            return SimplifiedFailed;
        if (IsCompleted(p))
            return SimplifiedCompleted;
        return SimplifiedProcessing;
    }
}

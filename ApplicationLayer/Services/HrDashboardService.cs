using ApplicationLayer.DTOs.Hr;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services;

/// <summary>SCRUM-339: gộp dữ liệu job sinh câu hỏi (JobStatus) + recommendation thành 1 response cho /hr/dashboard.</summary>
public class HrDashboardService : IHrDashboardService
{
    private const string SimplifiedCompleted = "COMPLETED";
    private const string SimplifiedFailed = "FAILED";
    private const string SimplifiedProcessing = "PROCESSING";

    private readonly IQuestionGenerationJobRepository _jobRepository;
    private readonly ICandidateRecommendationRepository _recommendationRepository;

    public HrDashboardService(
        IQuestionGenerationJobRepository jobRepository,
        ICandidateRecommendationRepository recommendationRepository)
    {
        _jobRepository = jobRepository;
        _recommendationRepository = recommendationRepository;
    }

    public async Task<HrDashboardResponseDto> GetDashboardAsync(Guid hrUserId, HrDashboardQueryDto query)
    {
        var activityDays = Math.Clamp(query.ActivityDays <= 0 ? 30 : query.ActivityDays, 1, 365);
        var recentLimit = Math.Clamp(query.RecentLimit <= 0 ? 7 : query.RecentLimit, 1, 50);
        var recommendationsLimit = Math.Clamp(query.RecommendationsLimit <= 0 ? 8 : query.RecommendationsLimit, 1, 50);

        var jobs = await _jobRepository.GetAllByOwnerWithPlanAndQuestionsAsync(hrUserId);
        var roleTitleByJobId = jobs.ToDictionary(j => j.Id, ResolveRoleTitle);

        var totalSessions = jobs.Count;
        var completedSessions = jobs.Count(j => j.Status == QuestionGenerationJobStatus.Completed);
        var totalQuestionsGenerated = jobs.Sum(j => j.Questions.Count);
        var successRatePercent = totalSessions == 0
            ? 0
            : Math.Round((double)completedSessions / totalSessions * 100, 2);

        var nowUtc = DateTime.UtcNow;
        var thisMonthSessions = jobs.Count(j => j.CreatedAt.Year == nowUtc.Year && j.CreatedAt.Month == nowUtc.Month);

        var topRole = roleTitleByJobId.Values
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .GroupBy(r => r!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        var dailyActivity = BuildDailyActivity(jobs, activityDays, nowUtc.Date);

        var questionTypeDistribution = jobs
            .SelectMany(j => j.Questions)
            .Select(q => QuestionTypeNormalizer.Normalize(new[] { q.QuestionType }).First())
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .Select(g => new HrDashboardQuestionTypeDistributionItemDto { Type = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var topQuestionType = questionTypeDistribution.FirstOrDefault()?.Type;

        var recentSessions = jobs
            .OrderByDescending(j => j.CreatedAt)
            .Take(recentLimit)
            .Select(j => new HrDashboardRecentSessionDto
            {
                Id = j.Id,
                RoleTitle = roleTitleByJobId[j.Id],
                Status = MapSimplifiedStatus(j.Status),
                QuestionCount = j.Questions.Count,
                CreatedAt = j.CreatedAt
            })
            .ToList();

        var thisWeekStart = nowUtc.Date.AddDays(-6);
        var lastWeekStart = thisWeekStart.AddDays(-7);
        var thisWeekCount = jobs.Count(j => j.CreatedAt.Date >= thisWeekStart);
        var lastWeekCount = jobs.Count(j => j.CreatedAt.Date >= lastWeekStart && j.CreatedAt.Date < thisWeekStart);
        var weekOverWeekDelta = thisWeekCount - lastWeekCount;
        var weekOverWeekTrend = weekOverWeekDelta > 0 ? "up" : weekOverWeekDelta < 0 ? "down" : "flat";

        var (recommendationRows, _) = await _recommendationRepository.ListByHrAsync(
            hrUserId, page: 1, pageSize: recommendationsLimit, status: null, questionSetId: null);

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
            Subscription = new HrDashboardSubscriptionDto()
        };
    }

    private static List<HrDashboardDailyActivityItemDto> BuildDailyActivity(
        IReadOnlyList<QuestionGenerationJob> jobs, int activityDays, DateTime todayUtc)
    {
        var fromDate = todayUtc.AddDays(-(activityDays - 1));
        var countsByDay = jobs
            .GroupBy(j => j.CreatedAt.Date)
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

    private static string? ResolveRoleTitle(QuestionGenerationJob job)
    {
        if (job.Plan is null)
            return null;

        var summary = PlanJsonSummaryReader.Read(job, job.Plan);
        return string.IsNullOrWhiteSpace(summary.Role) ? null : summary.Role;
    }

    private static string MapSimplifiedStatus(string rawStatus) => rawStatus switch
    {
        QuestionGenerationJobStatus.Completed => SimplifiedCompleted,
        QuestionGenerationJobStatus.Failed => SimplifiedFailed,
        QuestionGenerationJobStatus.Cancelled => SimplifiedFailed,
        _ => SimplifiedProcessing
    };
}

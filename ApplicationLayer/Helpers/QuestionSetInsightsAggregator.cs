using ApplicationLayer.DTOs.QuestionSet;

namespace ApplicationLayer.Helpers;

/// <summary>
/// SCRUM-513: gộp phiên COMPLETED + điểm AI thành KPI, bảng câu, leaderboard, lịch sử gần đây.
/// "Đạt" = Score &gt;= ngưỡng recommend của bộ. Không đụng DB.
/// </summary>
public static class QuestionSetInsightsAggregator
{
    public const int QualitySampleMin = 3;
    public const double TooEasyPassRate = 0.9;
    public const double TooHardPassRate = 0.3;
    public const int LeaderboardLimit = 20;
    public const int RecentAttemptsLimit = 10;

    public static QuestionSetInsightsDto Build(
        Guid questionSetId,
        bool isHiringAssessment,
        double passThreshold,
        bool includePractice,
        IReadOnlyList<QuestionInsightQuestionInput> questions,
        IReadOnlyList<QuestionSetInsightSessionRow> sessions,
        IReadOnlyList<QuestionSetInsightAnswerRow> answers)
    {
        // Bộ Tuyển mặc định chỉ bài official — phiên luyện chỉ vào khi HR bật includePractice.
        IEnumerable<QuestionSetInsightSessionRow> scoped = sessions;
        if (isHiringAssessment && !includePractice)
            scoped = sessions.Where(s => s.IsOfficialTest);

        var scopedList = scoped.ToList();
        var sessionIds = scopedList.Select(s => s.SessionId).ToHashSet();
        var scopedAnswers = answers
            .Where(a => sessionIds.Contains(a.SessionId))
            .ToList();

        var byQuestion = scopedAnswers
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var questionItems = questions
            .OrderBy(q => q.Order)
            .Select(q => BuildQuestion(q, byQuestion, passThreshold))
            .ToList();

        var scoredSessions = scopedList.Where(s => s.OverallScore.HasValue).ToList();
        double? averageScore = scoredSessions.Count == 0
            ? null
            : Math.Round(scoredSessions.Average(s => s.OverallScore!.Value), 1);

        var evaluatedCount = scopedAnswers.Count;
        var overallPass = scopedAnswers.Count(a => a.Score >= passThreshold);

        return new QuestionSetInsightsDto
        {
            QuestionSetId = questionSetId,
            IsHiringAssessment = isHiringAssessment,
            PassThreshold = passThreshold,
            Summary = new QuestionSetInsightsSummaryDto
            {
                CompletedCount = scopedList.Count,
                AverageScore = averageScore,
                PassRateOverall = Ratio(overallPass, evaluatedCount),
                EvaluatedAnswerCount = evaluatedCount
            },
            Questions = questionItems,
            Leaderboard = BuildLeaderboard(scopedList),
            RecentAttempts = scopedList
                .OrderByDescending(s => s.CompletedAt ?? DateTime.MinValue)
                .ThenByDescending(s => s.SessionId)
                .Take(RecentAttemptsLimit)
                .Select(s => new RecentAttemptItemDto
                {
                    SessionId = s.SessionId,
                    CandidateUserId = s.CandidateUserId,
                    CandidateName = s.CandidateName,
                    OverallScore = s.OverallScore.HasValue ? Math.Round(s.OverallScore.Value, 1) : null,
                    CompletedAt = s.CompletedAt,
                    IsOfficialTest = s.IsOfficialTest
                })
                .ToList()
        };
    }

    private static QuestionInsightItemDto BuildQuestion(
        QuestionInsightQuestionInput question,
        Dictionary<Guid, List<QuestionSetInsightAnswerRow>> byQuestion,
        double passThreshold)
    {
        var list = byQuestion.TryGetValue(question.Id, out var found)
            ? found
            : new List<QuestionSetInsightAnswerRow>();
        var evaluated = list.Count;
        var pass = list.Count(a => a.Score >= passThreshold);
        var rawRate = evaluated == 0 ? 0d : (double)pass / evaluated;

        string? flag = null;
        if (evaluated >= QualitySampleMin)
        {
            if (rawRate >= TooEasyPassRate) flag = "tooEasy";
            else if (rawRate <= TooHardPassRate) flag = "tooHard";
        }

        return new QuestionInsightItemDto
        {
            QuestionId = question.Id,
            Order = question.Order,
            QuestionText = question.QuestionText,
            Skill = question.Skill,
            Difficulty = question.Difficulty,
            EvaluatedCount = evaluated,
            AverageScore = evaluated == 0
                ? null
                : Math.Round(list.Average(a => a.Score), 1),
            PassCount = pass,
            PassRate = Ratio(pass, evaluated),
            FailRate = Ratio(evaluated - pass, evaluated),
            QualityFlag = flag
        };
    }

    private static List<LeaderboardItemDto> BuildLeaderboard(List<QuestionSetInsightSessionRow> scoped)
    {
        return scoped
            .GroupBy(s => s.CandidateUserId)
            .Select(g =>
            {
                var best = g
                    .Where(s => s.OverallScore.HasValue)
                    .OrderByDescending(s => s.OverallScore)
                    .ThenByDescending(s => s.CompletedAt ?? DateTime.MinValue)
                    .FirstOrDefault();
                var latest = g
                    .OrderByDescending(s => s.CompletedAt ?? DateTime.MinValue)
                    .First();
                return new
                {
                    CandidateUserId = g.Key,
                    CandidateName = (best ?? latest).CandidateName,
                    Best = best?.OverallScore,
                    AttemptCount = g.Count(),
                    LatestCompletedAt = g.Max(s => s.CompletedAt),
                    IsOfficialTest = (best ?? latest).IsOfficialTest
                };
            })
            .Where(x => x.Best.HasValue)
            .OrderByDescending(x => x.Best)
            .ThenByDescending(x => x.LatestCompletedAt ?? DateTime.MinValue)
            .Take(LeaderboardLimit)
            .Select((x, i) => new LeaderboardItemDto
            {
                Rank = i + 1,
                CandidateUserId = x.CandidateUserId,
                CandidateName = x.CandidateName,
                BestOverallScore = Math.Round(x.Best!.Value, 1),
                AttemptCount = x.AttemptCount,
                LatestCompletedAt = x.LatestCompletedAt,
                IsOfficialTest = x.IsOfficialTest
            })
            .ToList();
    }

    private static double Ratio(int part, int total)
        => total == 0 ? 0 : Math.Round((double)part / total, 3);
}

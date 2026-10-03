using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Hr;

public class QuestionSetInsightsAggregatorTests
{
    private static readonly Guid SetId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid QEasy = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid QHard = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CandA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CandB = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Build_HappyPath_RanksQuestionsAndLeaderboard()
    {
        var officialA = Guid.NewGuid();
        var officialB = Guid.NewGuid();
        var practiceA = Guid.NewGuid();

        var dto = QuestionSetInsightsAggregator.Build(
            SetId,
            isHiringAssessment: false,
            passThreshold: 70,
            includePractice: false,
            Questions(),
            new[]
            {
                Session(officialA, CandA, "An", 88, official: true, at: new DateTime(2026, 10, 1)),
                Session(officialB, CandB, "Binh", 60, official: true, at: new DateTime(2026, 10, 2)),
                Session(practiceA, CandA, "An", 96, official: false, at: new DateTime(2026, 10, 3))
            },
            new[]
            {
                Answer(officialA, QEasy, 95),
                Answer(officialB, QEasy, 92),
                Answer(practiceA, QEasy, 91),
                Answer(officialA, QHard, 40),
                Answer(officialB, QHard, 50),
                Answer(practiceA, QHard, 20)
            });

        Assert.Equal(70, dto.PassThreshold);
        Assert.Equal(3, dto.Summary.CompletedCount);
        Assert.Equal(6, dto.Summary.EvaluatedAnswerCount);
        Assert.Equal(0.5, dto.Summary.PassRateOverall);

        var easy = Assert.Single(dto.Questions, q => q.QuestionId == QEasy);
        Assert.Equal("tooEasy", easy.QualityFlag);
        Assert.Equal(1, easy.PassRate);
        Assert.Equal(3, easy.PassCount);

        var hard = Assert.Single(dto.Questions, q => q.QuestionId == QHard);
        Assert.Equal("tooHard", hard.QualityFlag);
        Assert.Equal(0, hard.PassRate);
        Assert.Equal(1, hard.FailRate);

        Assert.Equal(CandA, dto.Leaderboard[0].CandidateUserId);
        Assert.Equal(96, dto.Leaderboard[0].BestOverallScore);
        Assert.Equal(2, dto.Leaderboard[0].AttemptCount);
        Assert.False(dto.Leaderboard[0].IsOfficialTest);
        Assert.Equal(2, dto.Leaderboard[1].Rank);
        Assert.Equal(practiceA, dto.RecentAttempts[0].SessionId);
    }

    [Fact]
    public void Build_HiringWithoutPractice_DropsUnofficialSessions()
    {
        var official = Guid.NewGuid();
        var practice = Guid.NewGuid();

        var dto = QuestionSetInsightsAggregator.Build(
            SetId,
            isHiringAssessment: true,
            passThreshold: 70,
            includePractice: false,
            Questions(),
            new[]
            {
                Session(official, CandA, "An", 80, official: true, at: new DateTime(2026, 10, 1)),
                Session(practice, CandA, "An", 99, official: false, at: new DateTime(2026, 10, 2))
            },
            new[]
            {
                Answer(official, QEasy, 80),
                Answer(practice, QEasy, 10),
                Answer(practice, QHard, 10)
            });

        Assert.Equal(1, dto.Summary.CompletedCount);
        Assert.Equal(1, dto.Summary.EvaluatedAnswerCount);
        Assert.Equal(80, dto.Leaderboard[0].BestOverallScore);
        Assert.Equal(1, dto.Leaderboard[0].AttemptCount);
        Assert.True(dto.Leaderboard[0].IsOfficialTest);
        Assert.Null(dto.Questions.Single(q => q.QuestionId == QEasy).QualityFlag);
        Assert.Equal(0, dto.Questions.Single(q => q.QuestionId == QHard).EvaluatedCount);
    }

    [Fact]
    public void Build_Empty_ReturnsQuestionsWithZeroStats()
    {
        var dto = QuestionSetInsightsAggregator.Build(
            SetId,
            isHiringAssessment: true,
            passThreshold: 70,
            includePractice: true,
            Questions(),
            Array.Empty<QuestionSetInsightSessionRow>(),
            Array.Empty<QuestionSetInsightAnswerRow>());

        Assert.Equal(0, dto.Summary.CompletedCount);
        Assert.Null(dto.Summary.AverageScore);
        Assert.Equal(0, dto.Summary.PassRateOverall);
        Assert.Equal(2, dto.Questions.Count);
        Assert.All(dto.Questions, q =>
        {
            Assert.Equal(0, q.EvaluatedCount);
            Assert.Null(q.AverageScore);
            Assert.Null(q.QualityFlag);
        });
        Assert.Empty(dto.Leaderboard);
        Assert.Empty(dto.RecentAttempts);
    }

    private static List<QuestionInsightQuestionInput> Questions() => new()
    {
        new QuestionInsightQuestionInput
        {
            Id = QEasy, Order = 1, QuestionText = "Easy?", Skill = "C#", Difficulty = "Easy"
        },
        new QuestionInsightQuestionInput
        {
            Id = QHard, Order = 2, QuestionText = "Hard?", Skill = "SQL", Difficulty = "Hard"
        }
    };

    private static QuestionSetInsightSessionRow Session(
        Guid id, Guid candidate, string name, double score, bool official, DateTime at)
        => new()
        {
            SessionId = id,
            CandidateUserId = candidate,
            CandidateName = name,
            OverallScore = score,
            IsOfficialTest = official,
            CompletedAt = at
        };

    private static QuestionSetInsightAnswerRow Answer(Guid sessionId, Guid questionId, double score)
        => new() { SessionId = sessionId, QuestionId = questionId, Score = score };
}

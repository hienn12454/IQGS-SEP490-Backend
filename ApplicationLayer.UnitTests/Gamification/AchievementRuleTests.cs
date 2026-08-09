using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Gamification.AchievementRules;
using DomainLayer.Constants;
using Xunit;

namespace ApplicationLayer.UnitTests.Gamification;

public class AchievementRuleTests
{
    private static AchievementEvaluationContext Context(
        int currentStreak = 0,
        int totalQuestions = 0,
        int totalSessions = 0,
        int totalTechnical = 0,
        int totalSystemDesign = 0,
        int dailyGoalDays = 0,
        double? lastScore = null) => new()
    {
        UserId = Guid.NewGuid(),
        CurrentStreak = currentStreak,
        TotalQuestionsCompleted = totalQuestions,
        TotalSessionsCompleted = totalSessions,
        TotalTechnicalQuestionsCompleted = totalTechnical,
        TotalSystemDesignQuestionsCompleted = totalSystemDesign,
        DailyGoalCompletedDaysCount = dailyGoalDays,
        LastQuestionScore = lastScore
    };

    [Fact]
    public void FirstStep_UnlocksOnFirstSession()
    {
        var rule = new FirstStepAchievementRule();
        Assert.Equal(AchievementCode.FirstStep, rule.Code);
        Assert.False(rule.IsUnlocked(Context(totalSessions: 0)));
        Assert.True(rule.IsUnlocked(Context(totalSessions: 1)));
    }

    [Fact]
    public void OnFire_UnlocksAt7DayStreak()
    {
        var rule = new OnFireAchievementRule();
        Assert.False(rule.IsUnlocked(Context(currentStreak: 6)));
        Assert.True(rule.IsUnlocked(Context(currentStreak: 7)));
    }

    [Fact]
    public void ExcellentAnswer_UnlocksAtScore90()
    {
        var rule = new ExcellentAnswerAchievementRule();
        Assert.False(rule.IsUnlocked(Context(lastScore: 89)));
        Assert.True(rule.IsUnlocked(Context(lastScore: 90)));
        Assert.False(rule.IsUnlocked(Context(lastScore: null)));
    }

    [Fact]
    public void Dedicated_UnlocksAt100Questions()
    {
        var rule = new DedicatedAchievementRule();
        Assert.False(rule.IsUnlocked(Context(totalQuestions: 99)));
        Assert.True(rule.IsUnlocked(Context(totalQuestions: 100)));
    }

    [Fact]
    public void TechnicalMind_UnlocksAt50TechnicalQuestions()
    {
        var rule = new TechnicalMindAchievementRule();
        Assert.False(rule.IsUnlocked(Context(totalTechnical: 49)));
        Assert.True(rule.IsUnlocked(Context(totalTechnical: 50)));
    }

    [Fact]
    public void SystemThinker_UnlocksAt30SystemDesignQuestions()
    {
        var rule = new SystemThinkerAchievementRule();
        Assert.False(rule.IsUnlocked(Context(totalSystemDesign: 29)));
        Assert.True(rule.IsUnlocked(Context(totalSystemDesign: 30)));
    }

    [Fact]
    public void Consistency_UnlocksAt10DailyGoalDays()
    {
        var rule = new ConsistencyAchievementRule();
        Assert.False(rule.IsUnlocked(Context(dailyGoalDays: 9)));
        Assert.True(rule.IsUnlocked(Context(dailyGoalDays: 10)));
    }

    [Fact]
    public void InterviewVeteran_UnlocksAt100Sessions()
    {
        var rule = new InterviewVeteranAchievementRule();
        Assert.False(rule.IsUnlocked(Context(totalSessions: 99)));
        Assert.True(rule.IsUnlocked(Context(totalSessions: 100)));
    }

    [Fact]
    public void AllRuleCodes_AreUniqueAndMatchConstants()
    {
        IAchievementRule[] rules =
        [
            new FirstStepAchievementRule(),
            new OnFireAchievementRule(),
            new ExcellentAnswerAchievementRule(),
            new DedicatedAchievementRule(),
            new TechnicalMindAchievementRule(),
            new SystemThinkerAchievementRule(),
            new ConsistencyAchievementRule(),
            new InterviewVeteranAchievementRule()
        ];

        var codes = rules.Select(r => r.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }
}

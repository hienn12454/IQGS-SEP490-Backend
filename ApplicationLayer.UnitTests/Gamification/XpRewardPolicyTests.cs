using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Gamification;
using ApplicationLayer.Settings;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApplicationLayer.UnitTests.Gamification;

public class XpRewardPolicyTests
{
    private readonly IXpRewardPolicy _policy = new XpRewardPolicy(Options.Create(new GamificationOptions()));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(59, 0)]
    [InlineData(60, 2)]
    [InlineData(74, 2)]
    [InlineData(75, 4)]
    [InlineData(89, 4)]
    [InlineData(90, 6)]
    [InlineData(100, 6)]
    public void GetScoreBonus_MatchesSpecTiers(double score, int expectedXp)
    {
        Assert.Equal(expectedXp, _policy.GetScoreBonus(score));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void GetScoreBonus_OutOfRange_Throws(double score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _policy.GetScoreBonus(score));
    }

    [Theory]
    [InlineData(61, 70, false)] // +9 — chưa đủ
    [InlineData(61, 71, true)]  // +10 — đủ ngưỡng
    [InlineData(61, 72, true)]  // +11 — vượt ngưỡng
    public void IsImprovementEligible_MatchesTenPointThreshold(double previous, double current, bool expected)
    {
        Assert.Equal(expected, _policy.IsImprovementEligible(previous, current));
    }

    [Theory]
    [InlineData(3, true, 20)]
    [InlineData(7, true, 50)]
    [InlineData(14, true, 100)]
    [InlineData(30, true, 200)]
    [InlineData(1, false, 0)]
    [InlineData(5, false, 0)]
    [InlineData(31, false, 0)]
    public void TryGetStreakMilestoneXp_MatchesSpecMilestones(int streakDays, bool expectedFound, int expectedXp)
    {
        var found = _policy.TryGetStreakMilestoneXp(streakDays, out var xp);
        Assert.Equal(expectedFound, found);
        if (expectedFound)
            Assert.Equal(expectedXp, xp);
    }

    [Fact]
    public void FixedXpAmounts_MatchSpecDefaults()
    {
        Assert.Equal(10, _policy.QuestionCompletedXp);
        Assert.Equal(20, _policy.QuestionSetCompletedXp);
        Assert.Equal(5, _policy.ImprovementBonusXp);
    }
}

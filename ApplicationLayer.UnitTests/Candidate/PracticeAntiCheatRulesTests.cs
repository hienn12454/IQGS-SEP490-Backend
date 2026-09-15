using ApplicationLayer.Helpers;
using DomainLayer.Constants;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

/// <summary>SCRUM-446: quy tắc anti-cheat thuần — snapshot / debounce / auto-submit.</summary>
public class PracticeAntiCheatRulesTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 3)]
    [InlineData(20, 20)]
    [InlineData(99, 20)]
    [InlineData(-5, 1)]
    public void ClampMaxTabLeaves_ClampsToRange(int input, int expected)
    {
        Assert.Equal(expected, PracticeAntiCheatRules.ClampMaxTabLeaves(input));
    }

    [Fact]
    public void ShouldCountTabLeave_False_WhenAntiCheatOff()
    {
        var ok = PracticeAntiCheatRules.ShouldCountTabLeave(
            antiCheatEnabled: false,
            sessionStatus: PracticeSessionStatus.InProgress,
            lastTabLeaveAt: null,
            nowUtc: DateTime.UtcNow);

        Assert.False(ok);
    }

    [Fact]
    public void ShouldCountTabLeave_False_WhenNotInProgress()
    {
        var ok = PracticeAntiCheatRules.ShouldCountTabLeave(
            antiCheatEnabled: true,
            sessionStatus: PracticeSessionStatus.Completed,
            lastTabLeaveAt: null,
            nowUtc: DateTime.UtcNow);

        Assert.False(ok);
    }

    [Fact]
    public void ShouldCountTabLeave_False_WithinDebounce()
    {
        var now = DateTime.UtcNow;
        var ok = PracticeAntiCheatRules.ShouldCountTabLeave(
            antiCheatEnabled: true,
            sessionStatus: PracticeSessionStatus.InProgress,
            lastTabLeaveAt: now.AddSeconds(-1),
            nowUtc: now,
            debounce: TimeSpan.FromSeconds(2));

        Assert.False(ok);
    }

    [Fact]
    public void ShouldCountTabLeave_True_AfterDebounce()
    {
        var now = DateTime.UtcNow;
        var ok = PracticeAntiCheatRules.ShouldCountTabLeave(
            antiCheatEnabled: true,
            sessionStatus: PracticeSessionStatus.InProgress,
            lastTabLeaveAt: now.AddSeconds(-3),
            nowUtc: now,
            debounce: TimeSpan.FromSeconds(2));

        Assert.True(ok);
    }

    [Fact]
    public void ShouldCountTabLeave_True_WhenFirstLeave()
    {
        var ok = PracticeAntiCheatRules.ShouldCountTabLeave(
            antiCheatEnabled: true,
            sessionStatus: PracticeSessionStatus.InProgress,
            lastTabLeaveAt: null,
            nowUtc: DateTime.UtcNow);

        Assert.True(ok);
    }

    [Theory]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(4, 3, true)]
    [InlineData(1, 1, true)]
    public void ShouldAutoSubmit_WhenCountReachesMax(int count, int max, bool expected)
    {
        Assert.Equal(expected, PracticeAntiCheatRules.ShouldAutoSubmit(count, max));
    }
}

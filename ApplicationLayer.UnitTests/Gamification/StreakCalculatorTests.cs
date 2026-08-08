using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Gamification;
using Xunit;

namespace ApplicationLayer.UnitTests.Gamification;

public class StreakCalculatorTests
{
    private readonly IStreakCalculator _calculator = new StreakCalculator();
    private static readonly DateOnly Today = new(2026, 8, 9);

    [Fact]
    public void FirstEverActivity_StartsStreakAt1()
    {
        var result = _calculator.Compute(previousActiveLocalDate: null, previousCurrentStreak: 0, previousLongestStreak: 0, Today);

        Assert.Equal(1, result.CurrentStreak);
        Assert.Equal(1, result.LongestStreak);
        Assert.True(result.IsNewActiveDay);
    }

    [Fact]
    public void SameDayMultipleQuestions_DoesNotChangeStreak()
    {
        var result = _calculator.Compute(previousActiveLocalDate: Today, previousCurrentStreak: 4, previousLongestStreak: 6, Today);

        Assert.Equal(4, result.CurrentStreak);
        Assert.Equal(6, result.LongestStreak);
        Assert.False(result.IsNewActiveDay);
    }

    [Fact]
    public void ConsecutiveDay_IncrementsStreak()
    {
        var yesterday = Today.AddDays(-1);
        var result = _calculator.Compute(previousActiveLocalDate: yesterday, previousCurrentStreak: 4, previousLongestStreak: 4, Today);

        Assert.Equal(5, result.CurrentStreak);
        Assert.Equal(5, result.LongestStreak);
        Assert.True(result.IsNewActiveDay);
    }

    [Fact]
    public void MissedDay_ResetsStreakTo1ButKeepsLongest()
    {
        var threeDaysAgo = Today.AddDays(-3);
        var result = _calculator.Compute(previousActiveLocalDate: threeDaysAgo, previousCurrentStreak: 10, previousLongestStreak: 15, Today);

        Assert.Equal(1, result.CurrentStreak);
        Assert.Equal(15, result.LongestStreak);
        Assert.True(result.IsNewActiveDay);
    }

    [Fact]
    public void NewStreakExceedingPreviousLongest_UpdatesLongest()
    {
        var yesterday = Today.AddDays(-1);
        var result = _calculator.Compute(previousActiveLocalDate: yesterday, previousCurrentStreak: 6, previousLongestStreak: 6, Today);

        Assert.Equal(7, result.CurrentStreak);
        Assert.Equal(7, result.LongestStreak);
    }

    [Fact]
    public void TimezoneBoundary_ActivityJustAfterMidnightLocal_CountsAsNewDay()
    {
        // Giả lập: LastActiveLocalDate là "hôm qua" theo local date đã quy đổi — StreakCalculator chỉ nhận
        // DateOnly thuần, không quan tâm UTC offset (đã được IUserLocalDateProvider xử lý trước đó).
        var yesterday = Today.AddDays(-1);
        var result = _calculator.Compute(previousActiveLocalDate: yesterday, previousCurrentStreak: 1, previousLongestStreak: 1, Today);

        Assert.Equal(2, result.CurrentStreak);
        Assert.True(result.IsNewActiveDay);
    }
}

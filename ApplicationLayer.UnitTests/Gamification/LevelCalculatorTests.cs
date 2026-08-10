using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Gamification;
using ApplicationLayer.Settings;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApplicationLayer.UnitTests.Gamification;

public class LevelCalculatorTests
{
    private readonly ILevelCalculator _calculator = new LevelCalculator(Options.Create(new GamificationOptions()));

    [Fact]
    public void CalculateLevel_ZeroXp_IsLevel1()
    {
        Assert.Equal(1, _calculator.CalculateLevel(0));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(249, 2)]
    [InlineData(250, 3)]
    [InlineData(449, 3)]
    [InlineData(450, 4)]
    [InlineData(699, 4)]
    [InlineData(700, 5)]
    [InlineData(999, 5)]
    [InlineData(1000, 6)]
    [InlineData(1349, 6)]
    [InlineData(1350, 7)]
    public void CalculateLevel_MatchesSpecThresholds(long totalXp, int expectedLevel)
    {
        Assert.Equal(expectedLevel, _calculator.CalculateLevel(totalXp));
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 150)]
    [InlineData(3, 200)]
    [InlineData(4, 250)]
    [InlineData(5, 300)]
    [InlineData(6, 350)]
    public void GetXpRequiredForNextLevel_MatchesFormula(int level, long expectedDelta)
    {
        Assert.Equal(expectedDelta, _calculator.GetXpRequiredForNextLevel(level));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 100)]
    [InlineData(3, 250)]
    [InlineData(4, 450)]
    [InlineData(5, 700)]
    [InlineData(6, 1000)]
    [InlineData(7, 1350)]
    public void GetTotalXpForLevel_MatchesSpecTotals(int level, long expectedTotal)
    {
        Assert.Equal(expectedTotal, _calculator.GetTotalXpForLevel(level));
    }

    [Fact]
    public void CalculateLevel_VeryLargeXp_DoesNotThrowAndStaysConsistent()
    {
        const long hugeXp = 50_000_000L;
        var level = _calculator.CalculateLevel(hugeXp);

        Assert.True(_calculator.GetTotalXpForLevel(level) <= hugeXp);
        Assert.True(_calculator.GetTotalXpForLevel(level + 1) > hugeXp);
    }

    [Fact]
    public void GetCurrentLevelXp_And_ProgressPercentage_AreConsistent()
    {
        // Level 2 bắt đầu ở 100, cần 150 để lên level 3 → giữa chừng (100+75=175) phải là 50%.
        var currentLevelXp = _calculator.GetCurrentLevelXp(175);
        var progress = _calculator.GetProgressPercentage(175);

        Assert.Equal(75, currentLevelXp);
        Assert.Equal(50m, progress);
    }

    [Fact]
    public void GetProgressPercentage_NeverExceeds100()
    {
        var progress = _calculator.GetProgressPercentage(0);
        Assert.InRange(progress, 0m, 100m);
    }
}

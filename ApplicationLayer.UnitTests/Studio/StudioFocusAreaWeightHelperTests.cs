using ApplicationLayer.Studio.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

public sealed class StudioFocusAreaWeightHelperTests
{
    [Theory]
    [InlineData(0.4, 40)]
    [InlineData(0.5, 50)]
    [InlineData(40, 40)]
    [InlineData(100, 100)]
    [InlineData(0, 0)]
    public void NormalizeToPercent_ConvertsLegacyAndPercent(decimal input, decimal expected)
    {
        Assert.Equal(expected, StudioFocusAreaWeightHelper.NormalizeToPercent(input));
    }

    [Fact]
    public void IsValidSum_AcceptsNormalizedHundred()
    {
        Assert.True(StudioFocusAreaWeightHelper.IsValidSum([40m, 35m, 25m]));
    }
}

using ApplicationLayer.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

/// <summary>SCRUM-464: quy tắc bộ Tuyển vs Practice — AC snapshot + official complete.</summary>
public class HiringAssessmentRulesTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void ShouldEnableAntiCheat_RequiresHiringAndHrAndAdmin(
        bool isHiring,
        bool hrAc,
        bool adminAc,
        bool expected)
    {
        Assert.Equal(
            expected,
            HiringAssessmentRules.ShouldEnableAntiCheat(isHiring, hrAc, adminAc));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void IsFirstOfficialComplete_OnlyHiringWithoutPriorOfficial(
        bool isHiring,
        bool alreadyOfficial,
        bool expected)
    {
        Assert.Equal(
            expected,
            HiringAssessmentRules.IsFirstOfficialComplete(isHiring, alreadyOfficial));
    }
}

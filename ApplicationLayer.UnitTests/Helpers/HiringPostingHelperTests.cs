using ApplicationLayer.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Helpers;

/// <summary>SCRUM-468: gate metadata tin tuyển khi bật/publish bộ Tuyển.</summary>
public class HiringPostingHelperTests
{
    [Theory]
    [InlineData(false, null, null, null, null, null, true, false)]
    [InlineData(true, null, "BE", "IT", null, null, true, true)]
    [InlineData(true, "DN", null, "IT", null, null, true, true)]
    [InlineData(true, "DN", "BE", null, null, null, true, true)]
    [InlineData(true, "DN", "BE", "IT", null, null, true, false)]
    [InlineData(true, "DN", "BE", "IT", 10, 20, false, false)]
    [InlineData(true, "DN", "BE", "IT", null, null, false, true)]
    [InlineData(true, "  ", "BE", "IT", null, null, true, true)]
    public void RequiresHiringPostingFields_BlocksIncompleteHiring(
        bool isHiring,
        string? location,
        string? expertise,
        string? domain,
        int? salaryMin,
        int? salaryMax,
        bool negotiable,
        bool expected)
    {
        Assert.Equal(
            expected,
            HiringPostingHelper.RequiresHiringPostingFields(
                isHiring, location, expertise, domain, salaryMin, salaryMax, negotiable));
    }

    [Theory]
    [InlineData(null, null, true, true)]
    [InlineData(null, null, false, false)]
    [InlineData(10, 20, false, true)]
    [InlineData(20, 10, false, false)]
    [InlineData(0, 20, false, false)]
    [InlineData(10, null, false, true)]
    [InlineData(null, 20, false, true)]
    public void HasValidSalary_Rules(
        int? min, int? max, bool negotiable, bool expected)
    {
        Assert.Equal(expected, HiringPostingHelper.HasValidSalary(min, max, negotiable));
    }

    [Theory]
    [InlineData("AtOffice", "AtOffice")]
    [InlineData("hybrid", "Hybrid")]
    [InlineData("REMOTE", "Remote")]
    [InlineData("office", null)]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    public void NormalizeWorkplaceType_Cases(string? input, string? expected)
    {
        Assert.Equal(expected, HiringPostingHelper.NormalizeWorkplaceType(input));
    }

    [Fact]
    public void NormalizeOptionalText_TrimsAndNullsEmpty()
    {
        Assert.Null(HiringPostingHelper.NormalizeOptionalText("  ", 100));
        Assert.Equal("DN", HiringPostingHelper.NormalizeOptionalText("  DN  ", 100));
        Assert.Equal("abc", HiringPostingHelper.NormalizeOptionalText("abcdef", 3));
    }
}

using ApplicationLayer.Studio.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

/// <summary>Chuẩn hóa skills HR gửi qua PATCH metadata trước khi ghi DetectedSkillsJson.</summary>
public sealed class StudioHrSkillsNormalizeTests
{
    [Fact]
    public void Normalize_TrimsDedupesAndCapsLength()
    {
        var input = new[]
        {
            "  ASP.NET Core ",
            "asp.net core",
            "PostgreSQL",
            "",
            "   ",
            new string('x', 100),
        };
        var result = StudioHrSkillsHelper.Normalize(input);
        Assert.Equal(3, result.Length);
        Assert.Equal("ASP.NET Core", result[0]);
        Assert.Equal("PostgreSQL", result[1]);
        Assert.Equal(StudioHrSkillsHelper.MaxSkillLength, result[2].Length);
    }

    [Fact]
    public void Normalize_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(StudioHrSkillsHelper.Normalize(null));
        Assert.Empty(StudioHrSkillsHelper.Normalize([]));
    }

    [Fact]
    public void Normalize_MaxTwenty()
    {
        var many = Enumerable.Range(1, 30).Select(i => $"Skill{i}").ToList();
        var result = StudioHrSkillsHelper.Normalize(many);
        Assert.Equal(StudioHrSkillsHelper.MaxSkills, result.Length);
    }
}

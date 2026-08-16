using ApplicationLayer.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

public sealed class SkillMatchHelperTests
{
    [Fact]
    public void MatchPercent_IsIntersectionOverSetSkills()
    {
        var r = SkillMatchHelper.Compute(new[] { "C#", "SQL", "React" }, new[] { "c#", "python" });
        Assert.Equal(33, r.Percent);
        Assert.Single(r.Matched);
        Assert.Equal(2, r.Missing.Count);
    }

    [Fact]
    public void EmptySetSkills_ReturnsNullPercent()
    {
        var r = SkillMatchHelper.Compute(Array.Empty<string>(), new[] { "C#" });
        Assert.Null(r.Percent);
    }

    [Fact]
    public void UnionCvSkills_MergesTechStackAndEvaluationJson()
    {
        var json = """{"skills":["System Design","C#"]}""";
        var union = SkillMatchHelper.UnionCvSkills(new[] { "C#", "SQL" }, json);
        Assert.Contains(union, s => s.Equals("C#", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(union, s => s.Equals("SQL", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(union, s => s.Equals("System Design", StringComparison.OrdinalIgnoreCase));
    }
}

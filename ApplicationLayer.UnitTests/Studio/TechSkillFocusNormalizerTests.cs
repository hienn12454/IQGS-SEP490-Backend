using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

/// <summary>Map alias TechSkill và gộp focus trùng lúc tạo plan.</summary>
public sealed class TechSkillFocusNormalizerTests
{
    [Theory]
    [InlineData("csharp", "C#")]
    [InlineData("CSharp", "C#")]
    [InlineData("C#", "C#")]
    [InlineData("PostgreSQL", "PostgreSQL")]
    [InlineData("postgres", "PostgreSQL")]
    [InlineData("ASP.NET Core", "ASP.NET Core")]
    [InlineData("AspNetCore", "ASP.NET Core")]
    [InlineData("React.js", "React")]
    [InlineData("ReactJS", "React")]
    public void TryMap_Alias_ReturnsCanonicalLabel(string raw, string expected)
        => Assert.Equal(expected, TechSkillCatalog.TryMap(raw));

    [Fact]
    public void TryMap_JavaScript_DoesNotCollapseToJava()
    {
        Assert.Equal("JavaScript", TechSkillCatalog.TryMap("JavaScript"));
        Assert.Equal("Java", TechSkillCatalog.TryMap("Java"));
    }

    [Fact]
    public void TryMap_UnknownSkill_ReturnsNull()
        => Assert.Null(TechSkillCatalog.TryMap("Underwater Basket Weaving"));

    [Fact]
    public void Canonicalize_MergesCsharpAliases_IntoOneRow()
    {
        var merged = TechSkillFocusNormalizer.Canonicalize(
        [
            new StudioFocusAreaItemDto("C#", 40, 0),
            new StudioFocusAreaItemDto("CSharp", 20, 1),
            new StudioFocusAreaItemDto("React", 40, 2)
        ]);

        Assert.Equal(2, merged.Count);
        Assert.Equal("C#", merged[0].Name);
        Assert.Equal("React", merged[1].Name);
        Assert.Equal(100m, merged.Sum(f => f.Weight));
    }

    [Fact]
    public void SeedFromJd_SkipsUnknown_AndDedupesAlias()
    {
        var seeded = TechSkillFocusNormalizer.SeedFromJd(
            ["C#", "csharp", "PostgreSQL", "Not A Skill"]);

        Assert.Equal(2, seeded.Count);
        Assert.Contains(seeded, f => f.Name == "C#");
        Assert.Contains(seeded, f => f.Name == "PostgreSQL");
        Assert.Equal(100m, seeded.Sum(f => f.Weight));
    }

    [Fact]
    public void MergeDrafts_HrCsharp_PlusRagCsharp_DoesNotDuplicate()
    {
        var hr = new List<StudioFocusAreaItemDto>
        {
            new("C#", 100, 0)
        };
        var rag = new List<StudioRagPlanMapper.PlanFocusAreaDraft>
        {
            new("CSharp", 70, 1, ["job-description"]),
            new("React", 30, 2, ["system"])
        };

        var merged = TechSkillFocusNormalizer.MergeDrafts(rag, hr);

        Assert.Equal(2, merged.Count);
        Assert.Single(merged, f => f.Name == "C#");
        Assert.Single(merged, f => f.Name == "React");
        Assert.DoesNotContain(merged, f => f.Name == "Java");
        Assert.Equal(100m, merged.Sum(f => f.Weight));
    }

    [Fact]
    public void MergeDrafts_DoesNotAppendJdSkillMissingFromBothLists()
    {
        var hr = new List<StudioFocusAreaItemDto> { new("React", 100, 0) };
        var rag = Array.Empty<StudioRagPlanMapper.PlanFocusAreaDraft>();

        var merged = TechSkillFocusNormalizer.MergeDrafts(rag, hr);

        Assert.Single(merged);
        Assert.Equal("React", merged[0].Name);
    }
}

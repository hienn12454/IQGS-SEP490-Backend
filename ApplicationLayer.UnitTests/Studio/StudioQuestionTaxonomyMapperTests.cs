using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

public sealed class StudioQuestionTaxonomyMapperTests
{
    [Theory]
    [InlineData("system_design", "technical")]
    [InlineData("problem_solving", "technical")]
    [InlineData("behavioral", "behavioral")]
    [InlineData("Hard", "technical")]
    public void NormalizeCategory_MapsLegacyTypes(string input, string expected)
    {
        Assert.Equal(expected, StudioQuestionTaxonomyMapper.NormalizeCategory(input));
    }

    [Fact]
    public void LegacyTypeToCanonical_SystemDesign_IsTechnicalWithStyle()
    {
        var (cat, style) = StudioQuestionTaxonomyMapper.LegacyTypeToCanonical("system_design");
        Assert.Equal("technical", cat);
        Assert.Equal("system_design", style);
    }

    [Fact]
    public void FromLegacyQuestionTypes_DedupesSystemDesignAsTechnical()
    {
        var (dist, styles) = StudioQuestionTaxonomyMapper.FromLegacyQuestionTypes(
            ["technical", "system_design", "problem_solving", "behavioral"],
            totalQuestions: 10);

        Assert.Equal(2, dist.Count);
        Assert.Contains(dist, d => d.Category == "technical");
        Assert.Contains(dist, d => d.Category == "behavioral");
        Assert.Contains("system_design", styles);
        Assert.Contains("problem_solving", styles);
        Assert.Equal(10, dist.Sum(d => d.QuestionCount));
        Assert.Equal(100, dist.Sum(d => d.Percentage));
    }

    [Fact]
    public void RescaleQuestionCounts_FitsTotalWhenRagCountsOverflow()
    {
        var items = new List<QuestionDistributionItemDto>
        {
            new("technical", 83, 10),
            new("behavioral", 8, 1),
            new("situational", 8, 1)
        };

        StudioQuestionTaxonomyMapper.RescaleQuestionCounts(items, 10);

        Assert.Equal(10, items.Sum(d => d.QuestionCount));
        Assert.Equal(100, items.Sum(d => d.Percentage));
    }

    [Fact]
    public void ToLegacyQuestionTypes_ReturnsOnlyCanonicalCategories()
    {
        var legacy = StudioQuestionTaxonomyMapper.ToLegacyQuestionTypes(
        [
            new QuestionDistributionItemDto("technical", 60, 6),
            new QuestionDistributionItemDto("behavioral", 20, 2),
            new QuestionDistributionItemDto("situational", 20, 2)
        ]);

        Assert.All(legacy, c => Assert.Contains(c, StudioQuestionTaxonomyMapper.CanonicalCategories));
        Assert.Equal(3, legacy.Count);
    }
}

using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Services.Coach;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

public sealed class BlueprintComplianceValidatorTests
{
    private static BlueprintComplianceValidator.Slot Slot(int order, string skill, string difficulty)
        => new(order, skill, skill, difficulty);

    private static RagGeneratedQuestionDto Q(string skill, string difficulty, int order = 1)
        => new() { Skill = skill, Difficulty = difficulty, Order = order, Question = $"{skill} {difficulty}" };

    [Fact]
    public void Rejects_UnknownSkill()
    {
        var result = BlueprintComplianceValidator.Validate(
            [Slot(1, "Hooks", "easy"), Slot(2, "Hooks", "medium"), Slot(3, "Hooks", "hard")],
            [Q("SQL", "easy"), Q("Hooks", "medium"), Q("Hooks", "hard")]);
        Assert.False(result.Ok);
        Assert.Contains("ngoài blueprint", result.Error);
    }

    [Fact]
    public void Rejects_MissingCount()
    {
        var result = BlueprintComplianceValidator.Validate(
            [Slot(1, "Hooks", "easy"), Slot(2, "Hooks", "medium")],
            [Q("Hooks", "easy")]);
        Assert.False(result.Ok);
        Assert.Contains("1/2", result.Error);
    }

    [Fact]
    public void Maps_SkillAlias_AndNormalizesDifficulty()
    {
        var result = BlueprintComplianceValidator.Validate(
            [Slot(1, "EF Core", "medium")],
            [Q("ef-core", "Medium")]);
        Assert.True(result.Ok);
        Assert.Equal("EF Core", result.Questions[0].Skill);
        Assert.Equal("medium", result.Questions[0].Difficulty);
    }
}

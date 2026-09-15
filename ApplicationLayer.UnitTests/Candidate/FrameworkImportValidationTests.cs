using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Services.Coach;
using DomainLayer.Entities;
using Moq;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

public sealed class FrameworkImportValidationTests
{
    private static CompetencyFrameworkImportService Service(IReadOnlyList<CompetencyRoleAlias>? aliases = null)
    {
        var repo = new Mock<ICompetencyFrameworkRepository>();
        repo.Setup(r => r.ListAliasesAsync()).ReturnsAsync((aliases ?? []).ToList());
        return new CompetencyFrameworkImportService(repo.Object);
    }

    private static CompetencyFrameworkImportDto Payload(double weight = 0.5, string difficulty = "medium", string level = "Junior", int skillCount = 3)
    {
        var skills = Enumerable.Range(1, skillCount).Select(i => new CompetencyFrameworkSkillImportDto
        {
            Skill = $"Skill {i}",
            ImportanceWeight = i == skillCount ? 1 - weight * (skillCount - 1) : weight,
            TargetScore = 70,
            RequiredDifficulty = difficulty,
            Topics = [new CompetencyTopicImportDto { Topic = $"Topic {i}" }]
        }).ToList();
        return new CompetencyFrameworkImportDto
        {
            RoleKey = "java-backend",
            DisplayRole = "Java Backend",
            SourceRef = "fixture",
            SourceVersion = "test",
            Levels =
            [
                new CompetencyFrameworkLevelImportDto { Level = level, Skills = skills }
            ]
        };
    }

    [Fact]
    public async Task Rejects_WeightSumNotOne()
    {
        var dto = Payload();
        dto.Levels[0].Skills.ForEach(s => s.ImportanceWeight = 0.5);
        var result = await Service().ValidateAsync(dto);
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("importanceWeight"));
    }

    [Fact]
    public async Task Rejects_InvalidDifficultyAndLevel()
    {
        var dto = Payload(weight: 1.0 / 3, difficulty: "extreme", level: "Staff");
        dto.Levels[0].Skills[0].ImportanceWeight = 0.34;
        dto.Levels[0].Skills[1].ImportanceWeight = 0.33;
        dto.Levels[0].Skills[2].ImportanceWeight = 0.33;
        var result = await Service().ValidateAsync(dto);
        Assert.Contains(result.Errors, e => e.Contains("requiredDifficulty"));
        Assert.Contains(result.Errors, e => e.Contains("level"));
    }

    [Fact]
    public async Task Rejects_TooFewSkills()
    {
        var dto = Payload(skillCount: 2, weight: 0.5);
        var result = await Service().ValidateAsync(dto);
        Assert.Contains(result.Errors, e => e.Contains("tối thiểu 3"));
    }
}

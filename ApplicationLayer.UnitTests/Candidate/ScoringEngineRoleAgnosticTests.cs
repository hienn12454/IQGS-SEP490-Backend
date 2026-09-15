using ApplicationLayer.Services.Coach;
using DomainLayer.Entities;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

/// <summary>
/// Fixture in-memory — KHÔNG ghi DB. Cùng dimension scores, framework data khác → SkillScore/Overall khác.
/// </summary>
public sealed class ScoringEngineRoleAgnosticTests
{
    private static CompetencyScoringPolicy Policy() => new()
    {
        CorrectnessWeight = 0.5,
        RelevanceWeight = 0.3,
        ClarityWeight = 0.2,
        EasyDifficultyWeight = 1,
        MediumDifficultyWeight = 1.5,
        HardDifficultyWeight = 2,
        DevelopingMaxExclusive = 50,
        NearTargetMaxExclusive = 70,
        ReadyMaxExclusive = 85,
        JuniorReadyCoreSkillRatio = 0.7,
        OverallReadyThreshold = 70
    };

    private static List<CompetencyScoringService.AnswerInput> SameAnswers() =>
    [
        new("Hooks", "medium", 80, 80, 80, true),
        new("Hooks", "hard", 60, 60, 60, true),
        new("API", "medium", 40, 40, 40, true)
    ];

    [Fact]
    public void SameAnswers_DifferentFrameworkData_DifferentOverall()
    {
        var p = Policy();
        var dotnet = new List<CompetencyFrameworkSkill>
        {
            new() { Skill = "Hooks", ImportanceWeight = 0.7, TargetScore = 70, RequiredDifficulty = "medium", SortOrder = 1 },
            new() { Skill = "API", ImportanceWeight = 0.3, TargetScore = 65, RequiredDifficulty = "easy", SortOrder = 2 }
        };
        var react = new List<CompetencyFrameworkSkill>
        {
            new() { Skill = "Hooks", ImportanceWeight = 0.3, TargetScore = 80, RequiredDifficulty = "hard", SortOrder = 1 },
            new() { Skill = "API", ImportanceWeight = 0.7, TargetScore = 75, RequiredDifficulty = "medium", SortOrder = 2 }
        };

        var a = CompetencyScoringService.ComputeOverall(p, dotnet, SameAnswers());
        var b = CompetencyScoringService.ComputeOverall(p, react, SameAnswers());

        Assert.NotEqual(a.OverallReadiness, b.OverallReadiness);
        Assert.Equal(a.Skills.First(s => s.Skill == "Hooks").SkillScore,
            b.Skills.First(s => s.Skill == "Hooks").SkillScore);
        Assert.True(a.Skills.First(s => s.Skill == "Hooks").Gap <
                    b.Skills.First(s => s.Skill == "Hooks").Gap);
    }

    [Fact]
    public void AchievedLevel_FollowsFrameworkTargets_NotRoleLiteral()
    {
        var fwLow = new List<CompetencyFrameworkSkill>
        {
            new() { Skill = "A", TargetScore = 50, RequiredDifficulty = "easy", ImportanceWeight = 1 }
        };
        var fwHigh = new List<CompetencyFrameworkSkill>
        {
            new() { Skill = "A", TargetScore = 90, RequiredDifficulty = "hard", ImportanceWeight = 1 }
        };
        var profile = new List<CompetencyLevelRuleService.ProfileSkill> { new("A", 70, "medium") };
        var rules = CompetencyLevelRuleService.DefaultRules();

        var low = CompetencyLevelRuleService.Resolve(70, profile, fwLow, rules);
        var high = CompetencyLevelRuleService.Resolve(70, profile, fwHigh, rules);
        Assert.NotEqual(low.TargetMetRatio, high.TargetMetRatio);
    }
}

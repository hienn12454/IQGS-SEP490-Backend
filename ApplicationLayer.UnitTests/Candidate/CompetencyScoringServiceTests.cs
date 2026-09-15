using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

public sealed class CompetencyScoringServiceTests
{
    private static CompetencyScoringPolicy Policy() => new()
    {
        Id = CompetencyScoringPolicy.SingletonId,
        CorrectnessWeight = 0.5,
        RelevanceWeight = 0.3,
        ClarityWeight = 0.2,
        EasyDifficultyWeight = 1.0,
        MediumDifficultyWeight = 1.5,
        HardDifficultyWeight = 2.0,
        DevelopingMaxExclusive = 50,
        NearTargetMaxExclusive = 70,
        ReadyMaxExclusive = 85,
        JuniorReadyCoreSkillRatio = 0.7,
        OverallReadyThreshold = 70
    };

    [Fact]
    public void AnswerScore_IsDeterministicWeightedSum()
    {
        var score = CompetencyScoringService.ComputeAnswerScore(Policy(), 80, 60, 40);
        // 80*0.5 + 60*0.3 + 40*0.2 = 40 + 18 + 8 = 66
        Assert.Equal(66, score);
    }

    [Fact]
    public void SameDimensions_SameAnswerScore()
    {
        var p = Policy();
        var a = CompetencyScoringService.ComputeAnswerScore(p, 70, 70, 70);
        var b = CompetencyScoringService.ComputeAnswerScore(p, 70, 70, 70);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Difficulty_AffectsSkillScore()
    {
        var p = Policy();
        // Easy 100 vs Hard 100 — cùng AnswerScore nhưng Hard nặng hơn khi mix
        var easyOnly = CompetencyScoringService.ComputeSkillScore(p, [(50, "easy"), (100, "hard")]);
        var hardOnly = CompetencyScoringService.ComputeSkillScore(p, [(100, "easy"), (50, "hard")]);
        // (50*1 + 100*2)/3 = 250/3 ≈ 83.33
        // (100*1 + 50*2)/3 = 200/3 ≈ 66.67
        Assert.True(easyOnly > hardOnly);
        Assert.Equal(83.33, easyOnly);
        Assert.Equal(66.67, hardOnly);
    }

    [Fact]
    public void ReadinessStatus_UsesPolicyThresholds()
    {
        var p = Policy();
        Assert.Equal(CompetencyReadinessStatus.Developing, CompetencyScoringService.ResolveReadinessStatus(p, 49.9));
        Assert.Equal(CompetencyReadinessStatus.NearTarget, CompetencyScoringService.ResolveReadinessStatus(p, 50));
        Assert.Equal(CompetencyReadinessStatus.Ready, CompetencyScoringService.ResolveReadinessStatus(p, 70));
        Assert.Equal(CompetencyReadinessStatus.Strong, CompetencyScoringService.ResolveReadinessStatus(p, 85));
    }

    [Fact]
    public void Overall_WeightsFrameworkSkills()
    {
        var p = Policy();
        var skills = new List<CompetencyFrameworkSkill>
        {
            new() { Skill = "C#", ImportanceWeight = 0.5, TargetScore = 70, RequiredDifficulty = "medium", SortOrder = 1 },
            new() { Skill = "SQL", ImportanceWeight = 0.5, TargetScore = 65, RequiredDifficulty = "medium", SortOrder = 2 }
        };
        var answers = new List<CompetencyScoringService.AnswerInput>
        {
            new("C#", "medium", 80, 80, 80, true),
            new("SQL", "medium", 40, 40, 40, true)
        };
        var overall = CompetencyScoringService.ComputeOverall(p, skills, answers);
        Assert.Equal(60, overall.OverallReadiness); // (80+40)/2
        Assert.Equal(2, overall.Skills.Count);
        Assert.False(overall.MeetsJuniorReadyRule);
    }

    [Fact]
    public void FailedEvaluation_CountsAsZero()
    {
        var p = Policy();
        var skills = new List<CompetencyFrameworkSkill>
        {
            new() { Skill = "C#", ImportanceWeight = 1, TargetScore = 70, RequiredDifficulty = "medium", SortOrder = 1 }
        };
        var answers = new List<CompetencyScoringService.AnswerInput>
        {
            new("C#", "hard", 100, 100, 100, false)
        };
        var overall = CompetencyScoringService.ComputeOverall(p, skills, answers);
        Assert.Equal(0, overall.Skills[0].SkillScore);
    }

    [Fact]
    public void ExtractDimensions_MapsAliases()
    {
        var dims = CompetencyScoringService.ExtractDimensions(new Dictionary<string, double>
        {
            ["accuracy"] = 90,
            ["relevance"] = 80,
            ["clarity"] = 70
        });
        Assert.NotNull(dims);
        Assert.Equal(90, dims!["correctness"]);
        Assert.Equal(80, dims["relevance"]);
        Assert.Equal(70, dims["clarity"]);
    }
}

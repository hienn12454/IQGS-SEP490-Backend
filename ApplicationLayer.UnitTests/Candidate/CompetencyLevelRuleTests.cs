using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

public sealed class CompetencyLevelRuleTests
{
    [Fact]
    public void HighestLevel_RequiresAllFourConditions()
    {
        var fw = new List<CompetencyFrameworkSkill>
        {
            new() { Skill = "A", TargetScore = 60, RequiredDifficulty = "medium", ImportanceWeight = 0.5 },
            new() { Skill = "B", TargetScore = 60, RequiredDifficulty = "medium", ImportanceWeight = 0.5 }
        };
        var skills = new List<CompetencyLevelRuleService.ProfileSkill>
        {
            new("A", 90, "hard"),
            new("B", 90, "hard")
        };
        var result = CompetencyLevelRuleService.Resolve(90, skills, fw, CompetencyLevelRuleService.DefaultRules());
        Assert.Equal(CoachSeniorityLevel.Senior, result.AchievedLevel);
        // SCRUM-509: snapshot ngưỡng để FE map criteria — không đổi logic chấm.
        Assert.Equal(85, result.OverallThreshold);
        Assert.Equal(0.9, result.TargetMetThreshold);
        Assert.Equal(0.9, result.RequiredDifficultyThreshold);
        Assert.Equal(0.8, result.HardEvidenceThreshold);
        Assert.True(result.Overall >= result.OverallThreshold);
        Assert.True(result.TargetMetRatio >= result.TargetMetThreshold);
    }

    [Fact]
    public void MissingHardEvidence_BlocksMiddle()
    {
        var fw = new List<CompetencyFrameworkSkill>
        {
            new() { Skill = "A", TargetScore = 60, RequiredDifficulty = "easy", ImportanceWeight = 1 }
        };
        var skills = new List<CompetencyLevelRuleService.ProfileSkill> { new("A", 80, "medium") };
        var result = CompetencyLevelRuleService.Resolve(80, skills, fw, CompetencyLevelRuleService.DefaultRules());
        Assert.Equal(CoachSeniorityLevel.Junior, result.AchievedLevel);
        Assert.True(result.HardEvidenceRatio < 0.5);
    }
}

using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Moq;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

public sealed class CompetencyProfileMergeTests
{
    private static CompetencyScoringPolicy Policy() => new()
    {
        DevelopingMaxExclusive = 50,
        NearTargetMaxExclusive = 70,
        ReadyMaxExclusive = 85
    };

    [Fact]
    public async Task PartialReassessment_DoesNotDropOtherSkills_AndRecomputesOverallOnProfile()
    {
        var userId = Guid.NewGuid();
        var fwId = Guid.NewGuid();
        var plan = new CandidateSkillPlan
        {
            CandidateUserId = userId,
            FrameworkId = fwId,
            OverallReadiness = 60,
            Items =
            {
                new CandidateSkillPlanItem { Skill = "C#", CurrentScore = 50, TargetScore = 70, ImportanceWeight = 0.5 },
                new CandidateSkillPlanItem { Skill = "SQL", CurrentScore = 80, TargetScore = 70, ImportanceWeight = 0.5 }
            }
        };

        var plans = new Mock<ICandidateSkillPlanRepository>();
        plans.Setup(p => p.GetByCandidateUserIdAsync(userId)).ReturnsAsync(plan);
        plans.Setup(p => p.UpdateAsync(It.IsAny<CandidateSkillPlan>())).Returns(Task.CompletedTask);

        var frameworks = new Mock<ICompetencyFrameworkRepository>();
        frameworks.Setup(f => f.GetPolicyAsync()).ReturnsAsync(Policy());

        var rules = new Mock<ICompetencyLevelRuleRepository>();
        rules.Setup(r => r.ListAsync()).ReturnsAsync(CompetencyLevelRuleService.DefaultRules());

        var svc = new CompetencyProfileService(plans.Object, frameworks.Object, rules.Object);
        var fw = new CompetencyFramework
        {
            Id = fwId,
            RoleKey = "java-backend",
            TargetLevel = "Junior",
            Skills =
            {
                new CompetencyFrameworkSkill { Skill = "C#", TargetScore = 70, ImportanceWeight = 0.5, RequiredDifficulty = "medium" },
                new CompetencyFrameworkSkill { Skill = "SQL", TargetScore = 70, ImportanceWeight = 0.5, RequiredDifficulty = "medium" }
            }
        };
        var assessment = new CandidateAssessment
        {
            Id = Guid.NewGuid(),
            CandidateUserId = userId,
            FrameworkId = fwId,
            Kind = CandidateAssessmentKind.Reassessment,
            ScopeSkillsJson = """["C#"]""",
            SkillResults =
            {
                new CandidateAssessmentSkillResult { Skill = "C#", SkillScore = 75, TargetScore = 70, Gap = -5, ImportanceWeight = 0.5, DemonstratedDifficulty = "hard" }
            }
        };

        var merge = await svc.MergeAsync(userId, assessment, fw);

        Assert.Equal(2, merge.Profile.Items.Count);
        Assert.Equal(75, merge.Profile.Items.First(i => i.Skill == "C#").CurrentScore);
        Assert.Equal(80, merge.Profile.Items.First(i => i.Skill == "SQL").CurrentScore);
        Assert.Equal(77.5, merge.Profile.OverallReadiness);
        Assert.Single(merge.SkillDeltas);
        // Overall mới 77.5 − overall cũ 60 = 17.5 (SQL vẫn 80, C# 50→75).
        Assert.Equal(17.5, merge.OverallDelta);
    }
}

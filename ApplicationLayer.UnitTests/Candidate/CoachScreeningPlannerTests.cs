using ApplicationLayer.Services.Coach;
using DomainLayer.Entities;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

public sealed class CoachScreeningPlannerTests
{
    [Fact]
    public void SelectSkills_DropsMeasuredAndRoadmap_CapsAndOrdersByWeight()
    {
        var fw = new CompetencyFramework
        {
            Skills =
            {
                new CompetencyFrameworkSkill { Skill = "Java", ImportanceWeight = 0.9 },
                new CompetencyFrameworkSkill { Skill = "Redis", ImportanceWeight = 0.2 },
                new CompetencyFrameworkSkill { Skill = "Docker", ImportanceWeight = 0.5 }
            }
        };
        var selected = CoachScreeningPlanner.SelectSkills(
            ["C#", "Java", "Docker", "Redis", "Git"],
            alreadyMeasured: ["C#"],
            existingRoadmapSkills: ["Git"],
            fw,
            maxSkills: 2);

        Assert.Equal(2, selected.Count);
        Assert.Equal("Java", selected[0]);
        Assert.Equal("Docker", selected[1]);
        Assert.DoesNotContain("C#", selected);
        Assert.DoesNotContain("Git", selected);
    }

    [Fact]
    public void QuestionsPerSkill_RaisesWhenMinTotalNotMet()
    {
        var policy = new CompetencyScoringPolicy
        {
            DiagnosticQuestionsPerSkill = 3,
            DiagnosticMinTotalQuestions = 20
        };
        // 5 skill × 3 = 15 < 20 → tăng lên 4
        Assert.Equal(4, CoachDiagnosticPolicy.ResolveDiagnosticQuestionsPerSkill(policy, 5));
        Assert.Equal(3, CoachDiagnosticPolicy.ResolveDiagnosticQuestionsPerSkill(
            new CompetencyScoringPolicy { DiagnosticQuestionsPerSkill = 3 }, 5));
    }
}

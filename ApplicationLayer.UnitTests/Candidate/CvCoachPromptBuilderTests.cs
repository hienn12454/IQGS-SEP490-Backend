using ApplicationLayer.Helpers;
using ApplicationLayer.Services;
using DomainLayer.Constants;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

public sealed class CvCoachPromptBuilderTests
{
    [Fact]
    public void SyntheticJd_ContainsOnlyProvidedSkills()
    {
        var skills = new[] { "C#", "System Design" };
        var jd = CvCoachPromptBuilder.BuildSyntheticJd("Backend", "Junior", "Built APIs", skills);
        Assert.Contains("C#", jd);
        Assert.Contains("System Design", jd);
        Assert.Contains("only these", jd, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invent", jd.Split("Skills to assess")[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not invent job requirements outside this skill list", jd);
    }

    [Fact]
    public void DiagnosticHrNote_RequiresTenQuestionsAndRamp()
    {
        var note = CvCoachPromptBuilder.DiagnosticHrNote(new[] { "C#" });
        Assert.Contains("10", note);
        Assert.Contains("easy", note, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C#", note);
    }

    [Fact]
    public void BuildCvContext_IncludesSummaryAndSkills()
    {
        var ctx = CvCoachPromptBuilder.BuildCvContext("Built APIs", new[] { "C#" });
        Assert.Contains("Built APIs", ctx);
        Assert.Contains("C#", ctx);
    }
}

public sealed class CandidateSkillPlanScoringTests
{
    [Fact]
    public void ResolveStatus_DoneWhenAtOrAboveTarget()
    {
        Assert.Equal(CandidateSkillPlanItemStatus.Pending, CandidateSkillPlanService.ResolveStatus(null, 70));
        Assert.Equal(CandidateSkillPlanItemStatus.InProgress, CandidateSkillPlanService.ResolveStatus(69.9, 70));
        Assert.Equal(CandidateSkillPlanItemStatus.Done, CandidateSkillPlanService.ResolveStatus(70, 70));
    }
}

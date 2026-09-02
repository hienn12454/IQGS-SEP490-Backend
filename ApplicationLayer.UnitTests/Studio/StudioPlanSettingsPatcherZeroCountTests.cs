using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Helpers;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

public sealed class StudioPlanSettingsPatcherZeroCountTests
{
    [Fact]
    public void Apply_DoesNotThrow_WhenDistributionHasZeroCountCategory()
    {
        var source = new InterviewPlan
        {
            Title = "Backend interview",
            SeniorityLevel = "Mid",
            TotalQuestions = 15,
            InterviewLengthMinutes = 60,
            SourcePlanJson = """{"roleTitle":"Backend","summary":"Plan","totalQuestions":15,"difficulty":"medium"}"""
        };
        var types = new[] { "technical", "behavioral", "situational" };
        var distribution = new[]
        {
            new QuestionDistributionItemDto("technical", 70, 11),
            new QuestionDistributionItemDto("behavioral", 30, 4),
            new QuestionDistributionItemDto("situational", 0, 0)
        };

        var mapped = StudioPlanSettingsPatcher.Apply(
            source,
            sourceSections: [],
            sourceFocus: [],
            targetTotal: 15,
            targetDifficulty: QuestionDifficulty.Medium,
            targetMinutes: 60,
            questionTypes: types,
            canonicalDistribution: distribution);

        Assert.Equal(15, mapped.TotalQuestions);
        Assert.False(string.IsNullOrWhiteSpace(mapped.SourcePlanJson));
    }

    [Fact]
    public void Apply_RewritesCoverageToHrFocusOnly()
    {
        var source = new InterviewPlan
        {
            Title = "Backend interview",
            SeniorityLevel = "Mid",
            TotalQuestions = 12,
            InterviewLengthMinutes = 45,
            SourcePlanJson = """
                {"roleTitle":"Backend","summary":"Plan","totalQuestions":12,"difficulty":"medium",
                 "coverage":[
                   {"skill":"C#","questionCount":6,"focusAreas":["OOP"]},
                   {"skill":"SQL","questionCount":6,"focusAreas":["Queries"]}
                 ]}
                """
        };

        var mapped = StudioPlanSettingsPatcher.Apply(
            source,
            sourceSections: [],
            sourceFocus: [new StudioPlanSettingsPatcher.FocusInput("Git", 100, 0)],
            targetTotal: 12,
            targetDifficulty: QuestionDifficulty.Medium,
            targetMinutes: 45,
            questionTypes: ["technical", "behavioral"],
            canonicalDistribution:
            [
                new QuestionDistributionItemDto("technical", 80, 10),
                new QuestionDistributionItemDto("behavioral", 20, 2)
            ]);

        Assert.Contains("\"skill\":\"Git\"", mapped.SourcePlanJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"skill\":\"C#\"", mapped.SourcePlanJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"skill\":\"SQL\"", mapped.SourcePlanJson, StringComparison.Ordinal);
        Assert.All(mapped.FocusAreas, f => Assert.Equal("Git", f.Name));
    }
}

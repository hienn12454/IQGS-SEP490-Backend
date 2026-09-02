using System.Text.Json.Nodes;
using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Helpers;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

public sealed class StudioPlanSettingsPatcherAnswerMethodTests
{
    private static InterviewPlan MakeSource(int total = 10) => new()
    {
        Title = "Backend interview",
        SeniorityLevel = "Mid",
        TotalQuestions = total,
        InterviewLengthMinutes = 60,
        SourcePlanJson = $$"""{"roleTitle":"Backend","summary":"Plan","totalQuestions":{{total}},"difficulty":"medium"}"""
    };

    private static int CountAnswerMethod(string sourcePlanJson, string method)
    {
        var outline = StudioRagPlanMapper.ExtractOutlineItems(sourcePlanJson);
        return outline.Count(o =>
            string.Equals(o.AnswerMethod, method, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Apply_Mixed_AssignsHalfCodeSlots()
    {
        var mapped = StudioPlanSettingsPatcher.Apply(
            MakeSource(10),
            sourceSections: [],
            sourceFocus: [],
            targetTotal: 10,
            targetDifficulty: QuestionDifficulty.Medium,
            targetMinutes: 60,
            questionTypes: ["technical", "behavioral"],
            canonicalDistribution:
            [
                new QuestionDistributionItemDto("technical", 70, 7),
                new QuestionDistributionItemDto("behavioral", 30, 3)
            ],
            contentMode: "Mixed");

        Assert.Equal(5, CountAnswerMethod(mapped.SourcePlanJson, "Code"));
        Assert.Equal(5, CountAnswerMethod(mapped.SourcePlanJson, "Text"));
    }

    [Fact]
    public void Apply_TheoryOnly_AllText()
    {
        var mapped = StudioPlanSettingsPatcher.Apply(
            MakeSource(8),
            sourceSections: [],
            sourceFocus: [],
            targetTotal: 8,
            targetDifficulty: QuestionDifficulty.Medium,
            targetMinutes: 45,
            questionTypes: ["technical"],
            contentMode: "TheoryOnly");

        Assert.Equal(0, CountAnswerMethod(mapped.SourcePlanJson, "Code"));
        Assert.Equal(8, CountAnswerMethod(mapped.SourcePlanJson, "Text"));
    }

    [Fact]
    public void Apply_CodeOnly_AllCode()
    {
        var mapped = StudioPlanSettingsPatcher.Apply(
            MakeSource(8),
            sourceSections: [],
            sourceFocus: [],
            targetTotal: 8,
            targetDifficulty: QuestionDifficulty.Medium,
            targetMinutes: 45,
            questionTypes: ["technical", "coding"],
            canonicalDistribution:
            [
                new QuestionDistributionItemDto("technical", 50, 4),
                new QuestionDistributionItemDto("coding", 50, 4)
            ],
            contentMode: "CodeOnly");

        Assert.Equal(8, CountAnswerMethod(mapped.SourcePlanJson, "Code"));
        Assert.Equal(0, CountAnswerMethod(mapped.SourcePlanJson, "Text"));
    }

    [Fact]
    public void AssignOutlineAnswerMethods_Mixed_PrefersCodingTypes()
    {
        var outline = new JsonArray
        {
            new JsonObject { ["order"] = 1, ["type"] = "technical" },
            new JsonObject { ["order"] = 2, ["type"] = "coding" },
            new JsonObject { ["order"] = 3, ["type"] = "behavioral" },
            new JsonObject { ["order"] = 4, ["type"] = "problemsolving" }
        };

        StudioPlanSettingsPatcher.AssignOutlineAnswerMethods(outline, "Mixed");

        Assert.Equal("Text", outline[0]!["answerMethod"]!.GetValue<string>());
        Assert.Equal("Code", outline[1]!["answerMethod"]!.GetValue<string>());
        Assert.Equal("Text", outline[2]!["answerMethod"]!.GetValue<string>());
        Assert.Equal("Code", outline[3]!["answerMethod"]!.GetValue<string>());
    }

    [Fact]
    public void Apply_WithHrOutlineItems_KeepsHrAnswerMethod()
    {
        var outlineItems = new List<PlanOutlineItemDto>
        {
            new(1, "technical", "medium", "Git", "Git", "goal", "Text"),
            new(2, "coding", "medium", "Algo", "Algo", "goal", "Code"),
            new(3, "technical", "easy", "API", "API", "goal", "Text"),
            new(4, "technical", "hard", "SQL", "SQL", "goal", "Text"),
            new(5, "behavioral", "medium", "Team", "Team", "goal", "Text")
        };

        var mapped = StudioPlanSettingsPatcher.Apply(
            MakeSource(5),
            sourceSections: [],
            sourceFocus: [],
            targetTotal: 5,
            targetDifficulty: QuestionDifficulty.Medium,
            targetMinutes: 30,
            questionTypes: ["technical", "coding", "behavioral"],
            outlineItems: outlineItems,
            contentMode: "CodeOnly"); // bước 2: không ghi đè answerMethod HR

        Assert.Equal(1, CountAnswerMethod(mapped.SourcePlanJson, "Code"));
        Assert.Equal(4, CountAnswerMethod(mapped.SourcePlanJson, "Text"));
    }
}

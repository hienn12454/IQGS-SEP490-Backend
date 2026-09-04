using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Helpers;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

/// <summary>SCRUM-435: % focus → số slot Live Preview.</summary>
public sealed class StudioFocusWeightOutlineTests
{
    [Fact]
    public void LargestRemainder_FiftyPercent_GetsHalfSlots()
    {
        // C# 50% + 3 skill còn lại chia 50% → ~17% mỗi skill (làm tròn)
        var weights = new[] { 50, 17, 17, 16 };
        var counts = StudioProportionalAllocator.LargestRemainder(weights, 15);

        Assert.Equal(15, counts.Sum());
        Assert.InRange(counts[0], 7, 8); // ~50% of 15
    }

    [Fact]
    public void LargestRemainder_AllowsZero_WhenSkillsExceedTotal()
    {
        var weights = Enumerable.Repeat(8, 12).ToList(); // equal-ish
        var counts = StudioProportionalAllocator.LargestRemainder(weights, 10);

        Assert.Equal(10, counts.Sum());
        Assert.Equal(12, counts.Count);
        Assert.Contains(0, counts);
        Assert.Equal(10, counts.Count(c => c > 0));
    }

    [Fact]
    public void Apply_FocusFiftyPercent_OutlineSlotsMatch()
    {
        var source = new InterviewPlan
        {
            Title = "Backend",
            SeniorityLevel = "Mid",
            TotalQuestions = 15,
            InterviewLengthMinutes = 60,
            SourcePlanJson = """
                {
                  "roleTitle":"Backend",
                  "totalQuestions":15,
                  "difficulty":"medium",
                  "recommendedQuestionOutline":[
                    {"order":1,"type":"technical","difficulty":"medium","skill":"X","goal":"g"},
                    {"order":2,"type":"technical","difficulty":"medium","skill":"X","goal":"g"},
                    {"order":3,"type":"technical","difficulty":"medium","skill":"X","goal":"g"},
                    {"order":4,"type":"technical","difficulty":"medium","skill":"X","goal":"g"},
                    {"order":5,"type":"technical","difficulty":"medium","skill":"X","goal":"g"},
                    {"order":6,"type":"technical","difficulty":"medium","skill":"X","goal":"g"},
                    {"order":7,"type":"technical","difficulty":"medium","skill":"X","goal":"g"},
                    {"order":8,"type":"technical","difficulty":"medium","skill":"X","goal":"g"},
                    {"order":9,"type":"technical","difficulty":"medium","skill":"X","goal":"g"},
                    {"order":10,"type":"technical","difficulty":"medium","skill":"X","goal":"g"},
                    {"order":11,"type":"behavioral","difficulty":"medium","skill":"X","goal":"g"},
                    {"order":12,"type":"behavioral","difficulty":"medium","skill":"X","goal":"g"},
                    {"order":13,"type":"behavioral","difficulty":"medium","skill":"X","goal":"g"},
                    {"order":14,"type":"behavioral","difficulty":"medium","skill":"X","goal":"g"},
                    {"order":15,"type":"behavioral","difficulty":"medium","skill":"X","goal":"g"}
                  ]
                }
                """
        };

        var focus = new[]
        {
            new StudioPlanSettingsPatcher.FocusInput("C#", 50, 0),
            new StudioPlanSettingsPatcher.FocusInput("SQL", 17, 1),
            new StudioPlanSettingsPatcher.FocusInput("React", 17, 2),
            new StudioPlanSettingsPatcher.FocusInput("Git", 16, 3),
        };

        var mapped = StudioPlanSettingsPatcher.Apply(
            source,
            sourceSections: [],
            sourceFocus: focus,
            targetTotal: 15,
            targetDifficulty: QuestionDifficulty.Medium,
            targetMinutes: 60,
            questionTypes: ["technical", "behavioral"],
            canonicalDistribution:
            [
                new QuestionDistributionItemDto("technical", 67, 10),
                new QuestionDistributionItemDto("behavioral", 33, 5)
            ]);

        var outline = StudioRagPlanMapper.ExtractOutlineItems(mapped.SourcePlanJson);
        Assert.Equal(15, outline.Count);
        var csharp = outline.Count(o =>
            string.Equals(o.Skill, "C#", StringComparison.OrdinalIgnoreCase)
            || string.Equals(o.FocusArea, "C#", StringComparison.OrdinalIgnoreCase));
        Assert.InRange(csharp, 7, 8);
    }

    [Fact]
    public void Redistributor_EqualSplitFiveSkills_TenSlots()
    {
        var focus = Enumerable.Range(1, 5)
            .Select(i => new StudioRagPlanMapper.PlanFocusAreaDraft($"Skill{i}", 20m, i, ["job-description"]))
            .ToList();

        var outlineItems = string.Join(",", Enumerable.Range(1, 10).Select(i =>
            $"{{\"order\":{i},\"type\":\"technical\",\"difficulty\":\"medium\",\"skill\":\"Old\",\"goal\":\"g\"}}"));

        var mapped = new StudioRagPlanMapper.MappedPlan(
            Title: "T",
            Summary: null,
            TotalQuestions: 10,
            InterviewLengthMinutes: 40,
            SeniorityLevel: "mid",
            Difficulty: QuestionDifficulty.Medium,
            SourcePlanJson: $"{{\"roleTitle\":\"T\",\"totalQuestions\":10,\"recommendedQuestionOutline\":[{outlineItems}]}}",
            Sections: [],
            FocusAreas: focus,
            EasyCount: 3,
            MediumCount: 4,
            HardCount: 3);

        var result = StudioOutlineFocusRedistributor.ApplyFocusWeightsToOutline(mapped);
        var outline = StudioRagPlanMapper.ExtractOutlineItems(result.SourcePlanJson);
        Assert.Equal(10, outline.Count);

        var bySkill = outline.GroupBy(o => o.Skill).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(5, bySkill.Count);
        Assert.All(bySkill.Values, c => Assert.Equal(2, c));
    }
}

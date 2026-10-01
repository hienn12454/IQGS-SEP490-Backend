using System.Text.Json.Nodes;
using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Helpers;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

/// <summary>
/// HG01: Domain (skill), Why ask (goal) và nguồn của mỗi slot outline phải cùng một chủ đề.
/// Dữ liệu dựng lại từ bộ Fullstack Developer của HR21 (QA 29/09): 12 skill JD chia đều, 10 câu.
/// </summary>
public sealed class StudioOutlineCoherenceTests
{
    private static readonly string[] Hr21Skills =
    [
        "ASP.NET Core", "Node.js", "React.js", "Next.js", "PostgreSQL", "SQL Server",
        "MySQL", "C#", "TypeScript", "HTML", "CSS", "Docker"
    ];

    // (type, skill LLM lập, goal LLM lập) — goal slot 2, 3, 4, 5, 8, 9 lấy nguyên văn ảnh QA
    private static readonly (string Type, string Skill, string Goal)[] Hr21Outline =
    [
        ("technical", "C#", "Check C# OOP fundamentals (interfaces vs abstract classes)."),
        ("technical", "ASP.NET Core", "Evaluate ability to structure RESTful endpoints and use Minimal APIs."),
        ("technical", "Node.js", "Compare Node.js API implementation with .NET for specific use cases."),
        ("technical", "React.js", "Assess proficiency in building user interfaces and state management."),
        ("technical", "Next.js", "Understand when to use server-side rendering for performance/SEO."),
        ("technical", "PostgreSQL", "Optimize slow PostgreSQL queries with indexes and EXPLAIN."),
        ("technical", "TypeScript", "Use TypeScript types to make API contracts safer."),
        ("technical", "Docker", "Confirm basic knowledge of Dockerizing a fullstack application."),
        ("situational", "Debugging", "Evaluate the process of debugging an issue across frontend, backend, and DB."),
        ("behavioral", "Teamwork", "Assess collaboration when requirements change late."),
    ];

    [Fact]
    public void Redistributor_Hr21_KeepsSkillGoalAndSourceTogether()
    {
        var mapped = BuildMappedPlan(Hr21Outline, EqualFocus(Hr21Skills));

        var result = StudioOutlineFocusRedistributor.ApplyFocusWeightsToOutline(mapped, "English");
        var outline = StudioRagPlanMapper.ExtractOutlineItems(result.SourcePlanJson);

        Assert.Equal(10, outline.Count);
        for (var i = 0; i < outline.Count; i++)
        {
            // Bản cũ: câu 3 thành Domain React.js nhưng goal + nguồn vẫn là Node.js/.NET
            Assert.Equal(Hr21Outline[i].Skill, outline[i].Skill);
            Assert.Equal(Hr21Outline[i].Goal, outline[i].Goal);
            Assert.Equal("JD:" + Hr21Outline[i].Skill, outline[i].Citations![0].Excerpt);
            Assert.False(outline[i].Relabeled);
            Assert.Equal(Hr21Outline[i].Skill, outline[i].PlannedSkill);
        }
    }

    [Fact]
    public void Redistributor_SoftSlots_NeverGetTechnicalFocusSkill()
    {
        var mapped = BuildMappedPlan(Hr21Outline, EqualFocus(Hr21Skills));

        var result = StudioOutlineFocusRedistributor.ApplyFocusWeightsToOutline(mapped, "English");
        var outline = StudioRagPlanMapper.ExtractOutlineItems(result.SourcePlanJson);

        // Bản cũ: câu 9 situational = TypeScript, câu 10 behavioral = HTML
        Assert.Equal("Debugging", outline[8].Skill);
        Assert.Equal("Teamwork", outline[9].Skill);
    }

    [Fact]
    public void Redistributor_WhenQuotaForcesChange_RelabelsWholeSlot()
    {
        (string Type, string Skill, string Goal)[] crowded =
        [
            ("technical", "React", "Explain React hooks lifecycle."),
            ("technical", "React.js", "Optimize re-renders with memoization."),
            ("technical", "ReactJS", "Manage global state in a large React app."),
            ("technical", "Node.js", "Handle backpressure in Node streams."),
            ("behavioral", "Communication", "Explain a technical trade-off to a PM."),
        ];
        var focus = new List<StudioRagPlanMapper.PlanFocusAreaDraft>
        {
            new("React.js", 40m, 1, []),
            new("Node.js", 30m, 2, []),
            new("PostgreSQL", 30m, 3, []),
        };

        var result = StudioOutlineFocusRedistributor.ApplyFocusWeightsToOutline(
            BuildMappedPlan(crowded, focus), "Vietnamese");
        var outline = StudioRagPlanMapper.ExtractOutlineItems(result.SourcePlanJson);

        Assert.Equal(2, outline.Count(o => o.Skill == "React.js"));
        var relabeled = Assert.Single(outline, o => o.Relabeled);
        Assert.Equal("PostgreSQL", relabeled.Skill);
        Assert.Equal("Đánh giá kiến thức PostgreSQL thực tế cho vị trí này.", relabeled.Goal);
        Assert.Null(relabeled.Citations);
        Assert.Equal("PostgreSQL", relabeled.PlannedSkill);
        Assert.Equal("Communication", outline[4].Skill);
        Assert.True(StudioOutlineFocusRedistributor.HasRelabeledSlots(result.SourcePlanJson));
    }

    [Fact]
    public void Redistributor_CoverageCountsMatchOutline()
    {
        var mapped = BuildMappedPlan(Hr21Outline, EqualFocus(Hr21Skills), withCoverage: true);

        var result = StudioOutlineFocusRedistributor.ApplyFocusWeightsToOutline(mapped, "English");
        var coverage = JsonNode.Parse(result.SourcePlanJson)!["coverage"]!.AsArray();

        int CountOf(string skill) => coverage
            .First(c => c!["skill"]!.GetValue<string>() == skill)!["questionCount"]!.GetValue<int>();
        Assert.Equal(1, CountOf("Docker"));
        Assert.Equal(0, CountOf("HTML"));
        // Chỉ 8 slot kỹ thuật mang skill focus — chip coverage phải cộng đúng 8
        Assert.Equal(8, coverage.Sum(c => c!["questionCount"]!.GetValue<int>()));
    }

    [Fact]
    public void Guard_SkillChangedButGoalNot_RewritesGoalAndDropsOldSources()
    {
        // Đúng hình dạng HG01 câu 3: Domain React.js, goal + nguồn còn của Node.js
        const string json = """
            {"recommendedQuestionOutline":[
              {"order":1,"type":"technical","difficulty":"medium","skill":"React.js","focusArea":"React.js",
               "plannedSkill":"Node.js","goal":"Compare Node.js API implementation with .NET",
               "citations":[{"sourceFile":"job-description","excerpt":"JD:Node.js"}]}
            ]}
            """;

        var fixedJson = StudioOutlineCoherenceGuard.Apply(json, "English", out var fixedSlots);
        var slot = StudioRagPlanMapper.ExtractOutlineItems(fixedJson).Single();

        Assert.Equal(1, fixedSlots);
        Assert.Equal("React.js", slot.Skill);
        Assert.Equal("Assess hands-on React.js knowledge relevant to this role.", slot.Goal);
        Assert.Null(slot.Citations);
        Assert.True(slot.Relabeled);
        Assert.Equal("React.js", slot.PlannedSkill);
    }

    [Fact]
    public void Guard_SlotWithoutPlannedSkill_IsOnlyStamped()
    {
        const string json = """
            {"recommendedQuestionOutline":[
              {"order":1,"type":"technical","difficulty":"medium","skill":"React.js","goal":"Explain React hooks."}
            ]}
            """;

        var fixedJson = StudioOutlineCoherenceGuard.Apply(json, "English", out var fixedSlots);
        var slot = StudioRagPlanMapper.ExtractOutlineItems(fixedJson).Single();

        Assert.Equal(0, fixedSlots);
        Assert.Equal("Explain React hooks.", slot.Goal);
        Assert.Equal("React.js", slot.PlannedSkill);
        Assert.False(slot.Relabeled);
    }

    [Fact]
    public void Guard_SameSkillDifferentSpelling_IsNotAChange()
    {
        const string json = """
            {"recommendedQuestionOutline":[
              {"order":1,"type":"technical","difficulty":"medium","skill":"React.js","plannedSkill":"ReactJS","goal":"Explain React hooks."}
            ]}
            """;

        StudioOutlineCoherenceGuard.Apply(json, "English", out var fixedSlots);

        Assert.Equal(0, fixedSlots);
    }

    [Fact]
    public void Patcher_HrChangesSkillOnLivePreview_WhyAskFollowsNewSkill()
    {
        var source = new InterviewPlan
        {
            Title = "Fullstack",
            SeniorityLevel = "Mid",
            TotalQuestions = 5,
            InterviewLengthMinutes = 30,
            Language = "English",
            SourcePlanJson = """{"roleTitle":"Fullstack","totalQuestions":5,"recommendedQuestionOutline":[]}"""
        };
        var items = new List<PlanOutlineItemDto>
        {
            // FE mới: đổi skill → xoá Why ask cũ, plannedSkill = skill mới
            new(1, "technical", "medium", "Node.js", "Node.js", "", "Text", null, "Node.js", true),
            // Client cũ: đổi skill nhưng vẫn gửi Why ask của skill trước (plannedSkill = Next.js)
            new(2, "technical", "medium", "PostgreSQL", "PostgreSQL", "Understand SSR in Next.js", "Text", null, "Next.js"),
            new(3, "technical", "medium", "React.js", "React.js", "Explain hooks", "Text", null, "React.js"),
            new(4, "behavioral", "medium", "Teamwork", "Teamwork", "Handle conflicts", "Text"),
            new(5, "situational", "medium", "Debugging", "Debugging", "Debug across layers", "Text"),
        };

        var mapped = StudioPlanSettingsPatcher.Apply(
            source,
            sourceSections: [],
            sourceFocus: [],
            targetTotal: 5,
            targetDifficulty: QuestionDifficulty.Medium,
            targetMinutes: 30,
            questionTypes: ["technical", "behavioral", "situational"],
            outlineItems: items);
        var outline = StudioRagPlanMapper.ExtractOutlineItems(mapped.SourcePlanJson);

        Assert.Equal("Assess hands-on Node.js knowledge relevant to this role.", outline[0].Goal);
        Assert.True(outline[0].Relabeled);
        Assert.Equal("Assess hands-on PostgreSQL knowledge relevant to this role.", outline[1].Goal);
        Assert.Equal("Explain hooks", outline[2].Goal);
        Assert.Equal("Handle conflicts", outline[3].Goal);
    }

    [Fact]
    public void Patcher_ApplySettings_BehavioralSlotsKeepSoftLabels()
    {
        var source = new InterviewPlan
        {
            Title = "Fullstack",
            SeniorityLevel = "Mid",
            TotalQuestions = 6,
            InterviewLengthMinutes = 40,
            SourcePlanJson = """
                {"roleTitle":"Fullstack","totalQuestions":6,"recommendedQuestionOutline":[
                  {"order":1,"type":"technical","difficulty":"medium","skill":"React.js","goal":"Explain React hooks"},
                  {"order":2,"type":"technical","difficulty":"medium","skill":"Node.js","goal":"Explain the event loop"},
                  {"order":3,"type":"technical","difficulty":"medium","skill":"Docker","goal":"Containerize an API"},
                  {"order":4,"type":"technical","difficulty":"medium","skill":"SQL","goal":"Tune a slow query"},
                  {"order":5,"type":"behavioral","difficulty":"medium","skill":"HTML","goal":"Handle a conflict in the team"},
                  {"order":6,"type":"behavioral","difficulty":"medium","skill":"Teamwork","goal":"Give feedback to a peer"}
                ]}
                """
        };
        var focus = new[]
        {
            new StudioPlanSettingsPatcher.FocusInput("React.js", 50, 0),
            new StudioPlanSettingsPatcher.FocusInput("Node.js", 50, 1),
            new StudioPlanSettingsPatcher.FocusInput("HTML", 0, 2),
        };

        var mapped = StudioPlanSettingsPatcher.Apply(
            source,
            sourceSections: [],
            sourceFocus: focus,
            targetTotal: 6,
            targetDifficulty: QuestionDifficulty.Medium,
            targetMinutes: 40,
            questionTypes: ["technical", "behavioral"],
            canonicalDistribution:
            [
                new QuestionDistributionItemDto("technical", 67, 4),
                new QuestionDistributionItemDto("behavioral", 33, 2)
            ]);
        var outline = StudioRagPlanMapper.ExtractOutlineItems(mapped.SourcePlanJson);

        var technical = outline.Where(o => o.Type == "technical").ToList();
        Assert.Equal(2, technical.Count(o => o.Skill == "React.js"));
        Assert.Equal(2, technical.Count(o => o.Skill == "Node.js"));
        // Slot đầu mỗi skill giữ goal LLM viết riêng cho skill đó
        Assert.Contains(technical, o => o.Skill == "React.js" && o.Goal == "Explain React hooks");
        Assert.Contains(technical, o => o.Skill == "Node.js" && o.Goal == "Explain the event loop");
        // Bản cũ rải focus lên cả slot behavioral → "câu hành vi gắn Domain HTML"
        var behavioral = outline.Where(o => o.Type == "behavioral").ToList();
        Assert.Equal(2, behavioral.Count);
        Assert.All(behavioral, o => Assert.DoesNotContain(o.Skill, new[] { "HTML", "React.js", "Node.js" }));
        Assert.Contains(behavioral, o => o.Goal == "Handle a conflict in the team");
    }

    private static List<StudioRagPlanMapper.PlanFocusAreaDraft> EqualFocus(string[] skills)
    {
        // Giống StudioPlanFocusJdCompleter: skill JD chia đều 100%
        var weight = Math.Round(100m / skills.Length, 2);
        return skills
            .Select((skill, i) => new StudioRagPlanMapper.PlanFocusAreaDraft(skill, weight, i + 1, ["job-description"]))
            .ToList();
    }

    private static StudioRagPlanMapper.MappedPlan BuildMappedPlan(
        (string Type, string Skill, string Goal)[] slots,
        IReadOnlyList<StudioRagPlanMapper.PlanFocusAreaDraft> focus,
        bool withCoverage = false)
    {
        var outline = new JsonArray();
        for (var i = 0; i < slots.Length; i++)
        {
            outline.Add(new JsonObject
            {
                ["order"] = i + 1,
                ["type"] = slots[i].Type,
                ["difficulty"] = "medium",
                ["skill"] = slots[i].Skill,
                ["focusArea"] = slots[i].Skill,
                ["goal"] = slots[i].Goal,
                ["citations"] = new JsonArray(new JsonObject
                {
                    ["sourceFile"] = "job-description",
                    ["excerpt"] = "JD:" + slots[i].Skill
                })
            });
        }

        var root = new JsonObject
        {
            ["roleTitle"] = "Fullstack Developer",
            ["totalQuestions"] = slots.Length,
            ["recommendedQuestionOutline"] = outline
        };
        if (withCoverage)
        {
            var coverage = new JsonArray();
            foreach (var f in focus)
                coverage.Add(new JsonObject { ["skill"] = f.Name, ["questionCount"] = 1 });
            root["coverage"] = coverage;
        }

        return new StudioRagPlanMapper.MappedPlan(
            Title: "Fullstack Developer",
            Summary: null,
            TotalQuestions: slots.Length,
            InterviewLengthMinutes: 40,
            SeniorityLevel: "mid",
            Difficulty: QuestionDifficulty.Medium,
            SourcePlanJson: root.ToJsonString(),
            Sections: [],
            FocusAreas: focus,
            EasyCount: 0,
            MediumCount: slots.Length,
            HardCount: 0);
    }
}

using ApplicationLayer.Studio.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

/// <summary>SCRUM-434: Plan focus phải đủ mọi skill JD sau completer.</summary>
public sealed class StudioPlanFocusJdCompleterTests
{
    [Fact]
    public void MatchJdSkill_ExactHeadAndContains()
    {
        var catalog = new[] { "C#", "ASP.NET Core", "PostgreSQL", "React" };
        Assert.Equal("C#", StudioPlanFocusJdCompleter.MatchJdSkill("C#", catalog));
        Assert.Equal("C#", StudioPlanFocusJdCompleter.MatchJdSkill("C# — OOP", catalog));
        Assert.Equal("ASP.NET Core", StudioPlanFocusJdCompleter.MatchJdSkill("ASP.NET Core – Middleware", catalog));
        Assert.Equal("PostgreSQL", StudioPlanFocusJdCompleter.MatchJdSkill("Query tuning with PostgreSQL", catalog));
        Assert.Null(StudioPlanFocusJdCompleter.MatchJdSkill("Kubernetes", catalog));
    }

    [Fact]
    public void EnsureAllJdSkills_FillsMissingAndSumsWeightsTo100()
    {
        var jdSkills = Enumerable.Range(1, 12).Select(i => $"Skill{i}").ToList();
        var ragFocus = new List<StudioRagPlanMapper.PlanFocusAreaDraft>
        {
            new("Skill1 — deep dive", 0.3m, 1, ["doc-a.docx"]),
            new("Skill2", 0.2m, 2, ["job-description"]),
            new("Skill3", 0.2m, 3, ["doc-b.docx"]),
            new("Skill4", 0.15m, 4, []),
            new("Skill5", 0.15m, 5, ["doc-c.docx"]),
            new("Unrelated Topic", 0.1m, 6, ["noise.docx"]),
        };

        var mapped = new StudioRagPlanMapper.MappedPlan(
            Title: "T",
            Summary: null,
            TotalQuestions: 15,
            InterviewLengthMinutes: 60,
            SeniorityLevel: "mid",
            Difficulty: DomainLayer.Studio.Enums.QuestionDifficulty.Medium,
            SourcePlanJson: """
            {
              "roleTitle": "Backend",
              "total_questions": 15,
              "coverage": [
                { "skill": "Skill1", "question_count": 3, "source_files": ["doc-a.docx"] },
                { "skill": "Skill2", "question_count": 3, "source_files": ["job-description"] },
                { "skill": "Skill3", "question_count": 3, "source_files": ["doc-b.docx"] },
                { "skill": "Skill4", "question_count": 3, "source_files": [] },
                { "skill": "Skill5", "question_count": 3, "source_files": ["doc-c.docx"] }
              ]
            }
            """,
            Sections: [],
            FocusAreas: ragFocus,
            EasyCount: 5,
            MediumCount: 5,
            HardCount: 5);

        var result = StudioPlanFocusJdCompleter.EnsureAllJdSkills(mapped, jdSkills, 15);

        Assert.Equal(12, result.FocusAreas.Count);
        Assert.Equal(jdSkills, result.FocusAreas.Select(f => f.Name).ToList());
        Assert.Equal(100m, result.FocusAreas.Sum(f => f.Weight));

        // Skill RAG map đúng catalog + giữ source
        var s1 = result.FocusAreas.First(f => f.Name == "Skill1");
        Assert.Contains("doc-a.docx", s1.SourceFiles);

        // Skill thiếu có source job-description
        var s12 = result.FocusAreas.First(f => f.Name == "Skill12");
        Assert.Contains("job-description", s12.SourceFiles);

        // Coverage đồng bộ đủ 12 + tổng question_count = 15
        using var doc = System.Text.Json.JsonDocument.Parse(result.SourcePlanJson);
        var coverage = doc.RootElement.GetProperty("coverage");
        Assert.Equal(12, coverage.GetArrayLength());
        var sumQ = 0;
        foreach (var item in coverage.EnumerateArray())
        {
            sumQ += item.GetProperty("question_count").GetInt32();
        }
        Assert.Equal(15, sumQ);
    }

    [Fact]
    public void EnsureAllJdSkills_EmptyCatalog_NoChange()
    {
        var focus = new List<StudioRagPlanMapper.PlanFocusAreaDraft>
        {
            new("Only", 100m, 1, ["x"])
        };
        var mapped = new StudioRagPlanMapper.MappedPlan(
            "T", null, 10, 40, "mid",
            DomainLayer.Studio.Enums.QuestionDifficulty.Medium,
            "{}", [], focus, 3, 4, 3);

        var result = StudioPlanFocusJdCompleter.EnsureAllJdSkills(mapped, [], 10);
        Assert.Single(result.FocusAreas);
        Assert.Equal("Only", result.FocusAreas[0].Name);
    }
}

using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

public sealed class DiagnosticBlueprintTests
{
    private static CompetencyFrameworkSkill Skill(string name, double weight, string required, int order, params string[] topics)
        => new()
        {
            Id = Guid.NewGuid(),
            Skill = name,
            ImportanceWeight = weight,
            TargetScore = 70,
            RequiredDifficulty = required,
            SortOrder = order,
            TopicsJson = System.Text.Json.JsonSerializer.Serialize(
                topics.Select(t => new { topic = t, subtopics = Array.Empty<string>() }))
        };

    [Fact]
    public void Diagnostic_ThreeSkills_HasNineQuestions_AndHardEvidence()
    {
        var skills = new List<CompetencyFrameworkSkill>
        {
            Skill("Hooks", 0.4, "medium", 1, "useState", "useEffect"),
            Skill("Routing", 0.3, "easy", 2, "react-router"),
            Skill("State", 0.3, "medium", 3, "redux")
        };
        var questions = DiagnosticBlueprintBuilder.BuildDiagnosticQuestions(
            skills, s => CompetencyTopicParser.ParseTopicNames(s.TopicsJson));

        Assert.Equal(9, questions.Count);
        Assert.Contains(questions, q => q.Difficulty == QuestionDifficultyLevel.Hard);
        Assert.Contains(questions, q => q.Difficulty == QuestionDifficultyLevel.Easy);
        foreach (var skill in skills)
        {
            var group = questions.Where(q => q.Skill == skill.Skill).ToList();
            Assert.Equal(3, group.Count);
            Assert.Contains(group, q => q.Difficulty == DiagnosticBlueprintBuilder.NormalizeDifficulty(skill.RequiredDifficulty));
            Assert.Contains(group, q => q.Difficulty == DiagnosticBlueprintBuilder.StepUp(skill.RequiredDifficulty));
        }
    }

    [Fact]
    public void Screening_OneQuestionPerSkill_AtRequiredDifficulty()
    {
        var bp = new ApplicationLayer.DTOs.Coach.CompetencyBlueprint
        {
            TargetRole = "Backend",
            TargetLevel = "Junior",
            Competencies =
            {
                new ApplicationLayer.DTOs.Coach.CompetencyItem
                {
                    SkillName = "Java",
                    Category = CompetencyCategory.RoleCore,
                    Topics = { "Collections" },
                    TargetScore = 70
                },
                new ApplicationLayer.DTOs.Coach.CompetencyItem
                {
                    SkillName = "SQL",
                    Category = CompetencyCategory.Fundamental,
                    Topics = { "Index" },
                    TargetScore = 70
                }
            }
        };
        var plan = DiagnosticBlueprintBuilder.BuildScreening(bp, 1);
        var json = System.Text.Json.JsonSerializer.Serialize(plan);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("totalQuestions").GetInt32());
        var outline = doc.RootElement.GetProperty("recommendedQuestionOutline");
        Assert.Equal(2, outline.GetArrayLength());
        Assert.Equal("Java", outline[0].GetProperty("skill").GetString());
        Assert.Equal("medium", outline[0].GetProperty("difficulty").GetString());
        Assert.Equal("SQL", outline[1].GetProperty("skill").GetString());
        Assert.Equal("easy", outline[1].GetProperty("difficulty").GetString());
    }

    [Fact]
    public void Reassessment_HonorsQuestionsPerSkill_IndependentOfDiagnosticDefault()
    {
        var bp = new ApplicationLayer.DTOs.Coach.CompetencyBlueprint
        {
            TargetRole = "Backend",
            TargetLevel = "Junior",
            Competencies =
            {
                new ApplicationLayer.DTOs.Coach.CompetencyItem
                {
                    SkillName = "C#",
                    Category = CompetencyCategory.RoleCore,
                    Topics = { "OOP", "LINQ" },
                    TargetScore = 70
                }
            }
        };
        var plan = DiagnosticBlueprintBuilder.BuildDiagnostic(
            bp, new DiagnosticBlueprintBuilder.Options(QuestionsPerSkill: 5));
        var json = System.Text.Json.JsonSerializer.Serialize(plan);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(5, doc.RootElement.GetProperty("totalQuestions").GetInt32());
        Assert.Equal(3, CoachDiagnosticPolicy.ReassessmentQuestionsPerSkill(new CompetencyScoringPolicy()));
        Assert.Equal(8, CoachDiagnosticPolicy.ReassessmentQuestionsPerSkill(
            new CompetencyScoringPolicy { ReassessmentQuestionsPerSkill = 8 }));
    }
}

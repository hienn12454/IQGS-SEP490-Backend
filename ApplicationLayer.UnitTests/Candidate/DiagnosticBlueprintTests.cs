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
}

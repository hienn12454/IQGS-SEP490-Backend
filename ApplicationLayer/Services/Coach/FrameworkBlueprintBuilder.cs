using ApplicationLayer.DTOs.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

/// <summary>SCRUM-457: convert predefined framework → CompetencyBlueprint (không đổi số liệu curated).</summary>
public static class FrameworkBlueprintBuilder
{
    public static CompetencyBlueprint Build(
        CompetencyFramework framework,
        string? targetRole,
        IReadOnlyList<CompetencyFrameworkSkill> skills,
        CompetencyScoringPolicy policy)
    {
        var comps = skills
            .OrderBy(s => s.SortOrder)
            .Select(s => new CompetencyItem
            {
                SkillKey = CompetencyScoringService.NormalizeSkill(s.Skill).Replace(' ', '-'),
                SkillName = s.Skill,
                Category = CategoryFromDifficulty(s.RequiredDifficulty),
                Weight = s.ImportanceWeight,
                ExpectedLevel = framework.TargetLevel,
                TargetScore = s.TargetScore > 0
                    ? s.TargetScore
                    : CompetencyTargetScorePolicy.Resolve(policy, framework.TargetLevel),
                Topics = CompetencyTopicParser.ParseTopicNames(s.TopicsJson),
                Source = CompetencySourceMode.Framework
            })
            .ToList();

        NormalizeWeights(comps);

        return new CompetencyBlueprint
        {
            SchemaVersion = CompetencyBlueprintSchema.CurrentVersion,
            SourceMode = CompetencyResolutionMode.Framework,
            RoleKey = framework.RoleKey,
            TargetRole = string.IsNullOrWhiteSpace(targetRole) ? framework.DisplayRole : targetRole.Trim(),
            TargetLevel = framework.TargetLevel,
            FrameworkKey = framework.RoleKey,
            FrameworkId = framework.Id,
            Competencies = comps,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static List<CompetencyFrameworkSkill> ToScoringSkills(CompetencyBlueprint blueprint)
    {
        var i = 0;
        return blueprint.Competencies.Select(c => new CompetencyFrameworkSkill
        {
            Id = Guid.NewGuid(),
            Skill = c.SkillName,
            ImportanceWeight = c.Weight,
            TargetScore = c.TargetScore,
            RequiredDifficulty = DifficultyFromCategory(c.Category),
            TopicsJson = System.Text.Json.JsonSerializer.Serialize(c.Topics),
            SortOrder = i++
        }).ToList();
    }

    public static string CategoryFromDifficulty(string? difficulty)
    {
        var d = DiagnosticBlueprintBuilder.NormalizeDifficulty(difficulty);
        if (d == QuestionDifficultyLevel.Easy) return CompetencyCategory.Fundamental;
        if (d == QuestionDifficultyLevel.Hard) return CompetencyCategory.Advanced;
        return CompetencyCategory.RoleCore;
    }

    public static string DifficultyFromCategory(string? category)
    {
        var c = (category ?? "").Trim().ToUpperInvariant();
        if (c == CompetencyCategory.Fundamental) return QuestionDifficultyLevel.Easy;
        if (c == CompetencyCategory.Advanced) return QuestionDifficultyLevel.Hard;
        return QuestionDifficultyLevel.Medium;
    }

    private static void NormalizeWeights(List<CompetencyItem> comps)
    {
        var sum = comps.Sum(c => c.Weight);
        if (sum <= 0 || comps.Count == 0) return;
        if (Math.Abs(sum - 1.0) <= 0.01) return;
        foreach (var c in comps)
            c.Weight = Math.Round(c.Weight / sum, 4);
    }
}

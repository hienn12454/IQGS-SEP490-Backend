using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services.Coach;

public interface IAdaptiveBlueprintBuilder
{
    Task<CompetencyBlueprint> BuildAsync(
        CompetencyResolution resolution,
        IReadOnlyList<string> cvSkills,
        CompetencyScoringPolicy policy,
        CancellationToken ct = default);
}

/// <summary>
/// SCRUM-457: Adaptive blueprint từ RAG + policy TargetScore.
/// Không fallback sang FRAMEWORK giả khi retrieval/LLM fail.
/// </summary>
public class AdaptiveBlueprintBuilder : IAdaptiveBlueprintBuilder
{
    private readonly IRagService _rag;

    public AdaptiveBlueprintBuilder(IRagService rag) => _rag = rag;

    public async Task<CompetencyBlueprint> BuildAsync(
        CompetencyResolution resolution,
        IReadOnlyList<string> cvSkills,
        CompetencyScoringPolicy policy,
        CancellationToken ct = default)
    {
        var context = await _rag.RetrieveCompetencyContextAsync(new RagCompetencyContextRequest
        {
            TargetRole = resolution.NormalizedRole,
            TargetLevel = resolution.TargetLevel,
            RoleFamilyKey = resolution.RoleFamilyKey,
            Skills = cvSkills.ToList()
        }, ct);

        if (!context.Success || context.Chunks.Count == 0)
            throw Fail(context.Error ?? "Không retrieve được Tech KB cho vai trò Adaptive.");

        var generated = await _rag.GenerateAdaptiveCompetencyBlueprintAsync(new RagAdaptiveBlueprintRequest
        {
            TargetRole = resolution.NormalizedRole,
            TargetLevel = resolution.TargetLevel,
            RoleFamilyKey = resolution.RoleFamilyKey,
            CvSkills = cvSkills.ToList(),
            Chunks = context.Chunks
        }, ct);

        if (!generated.Success || generated.Competencies.Count == 0)
            throw Fail(generated.Error ?? "LLM không sinh được competency blueprint hợp lệ.");

        var targetScore = CompetencyTargetScorePolicy.Resolve(policy, resolution.TargetLevel);
        var comps = generated.Competencies.Select(c => new CompetencyItem
        {
            SkillKey = string.IsNullOrWhiteSpace(c.SkillKey)
                ? CompetencyScoringService.NormalizeSkill(c.SkillName).Replace(' ', '-')
                : c.SkillKey.Trim(),
            SkillName = c.SkillName.Trim(),
            Category = NormalizeCategory(c.Category),
            Weight = c.Weight,
            ExpectedLevel = resolution.TargetLevel,
            TargetScore = targetScore,
            Topics = c.Topics.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList(),
            Source = CompetencySourceMode.Rag,
            Citations = c.Citations.Select(x => new CompetencyCitation
            {
                SourceTitle = x.SourceTitle,
                SourceUrl = x.SourceUrl,
                Section = x.Section,
                DocumentId = x.DocumentId,
                Excerpt = x.Excerpt
            }).ToList()
        }).ToList();

        var weightSum = comps.Sum(c => c.Weight);
        if (comps.Count is < 3 or > 8 || Math.Abs(weightSum - 1.0) > 0.01)
            throw Fail("Adaptive blueprint không đạt ràng buộc 3–8 skill / Σweight=1.");

        if (comps.Any(c => c.Topics.Count == 0))
            throw Fail("Mỗi competency Adaptive phải có ít nhất 1 topic từ retrieval.");

        return new CompetencyBlueprint
        {
            SchemaVersion = CompetencyBlueprintSchema.CurrentVersion,
            SourceMode = CompetencyResolutionMode.Adaptive,
            RoleKey = resolution.RoleKey ?? resolution.RoleFamilyKey ?? "adaptive",
            TargetRole = resolution.NormalizedRole,
            TargetLevel = resolution.TargetLevel,
            FrameworkKey = null,
            FrameworkId = null,
            Competencies = comps,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string NormalizeCategory(string? category)
    {
        var c = (category ?? "").Trim().ToUpperInvariant();
        if (c == CompetencyCategory.Fundamental) return CompetencyCategory.Fundamental;
        if (c == CompetencyCategory.Advanced) return CompetencyCategory.Advanced;
        return CompetencyCategory.RoleCore;
    }

    private static BadRequestException Fail(string detail)
        => new($"ADAPTIVE_BLUEPRINT_GENERATION_FAILED: {detail}");
}

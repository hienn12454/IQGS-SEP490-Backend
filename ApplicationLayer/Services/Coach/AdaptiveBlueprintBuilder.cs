using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
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
/// SCRUM-457 / SCRUM-461: Adaptive blueprint từ CV + RAG (chunk lệch stack bị drop).
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
        var skills = cvSkills
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (skills.Count == 0)
            throw Fail("Thiếu skill CV — không dựng Adaptive blueprint.");

        var context = await _rag.RetrieveCompetencyContextAsync(new RagCompetencyContextRequest
        {
            TargetRole = resolution.NormalizedRole,
            TargetLevel = resolution.TargetLevel,
            RoleFamilyKey = resolution.RoleFamilyKey,
            Skills = skills
        }, ct);

        // SCRUM-461: retrieval fail hoàn toàn → vẫn inferred từ CV (không giả FRAMEWORK).
        // success + chunks=[] (đã drop lệch stack) cũng đi inferred.
        var rawChunks = context.Success ? context.Chunks : new List<RagCompetencyChunkDto>();
        var chunks = CoachCvSkillGate.FilterChunks(rawChunks, skills);

        RagAdaptiveBlueprintResult? generated = null;
        try
        {
            generated = await _rag.GenerateAdaptiveCompetencyBlueprintAsync(new RagAdaptiveBlueprintRequest
            {
                TargetRole = resolution.NormalizedRole,
                TargetLevel = resolution.TargetLevel,
                RoleFamilyKey = resolution.RoleFamilyKey,
                CvSkills = skills,
                Chunks = chunks
            }, ct);
        }
        catch
        {
            // Giữ fallback CV bên dưới — không ném FRAMEWORK.
        }

        var targetScore = CompetencyTargetScorePolicy.Resolve(policy, resolution.TargetLevel);
        List<CompetencyItem> comps;

        if (generated is { Success: true } && generated.Competencies.Count > 0)
        {
            comps = MapAndGateCompetencies(generated.Competencies, skills, resolution.TargetLevel, targetScore);
            if (comps.Count == 0)
                comps = FallbackFromCv(skills, resolution.TargetLevel, targetScore);
        }
        else
        {
            // LLM fail / empty — deterministic từ CV, citations rỗng (inferred).
            comps = FallbackFromCv(skills, resolution.TargetLevel, targetScore);
        }

        var maxAllowed = Math.Min(8, skills.Count);
        if (comps.Count is < 1 || comps.Count > maxAllowed)
            throw Fail($"Adaptive blueprint không đạt ràng buộc 1–{maxAllowed} skill theo CV.");

        if (comps.Any(c => c.Topics.Count == 0))
            throw Fail("Mỗi competency Adaptive phải có ít nhất 1 topic.");

        RenormalizeWeights(comps);

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

    /// <summary>Map skill LLM → CV; drop skill lạ; gắn TargetScore.</summary>
    public static List<CompetencyItem> MapAndGateCompetencies(
        IEnumerable<RagAdaptiveCompetencyDto> generated,
        IReadOnlyList<string> cvSkills,
        string targetLevel,
        double targetScore)
    {
        var maxAllowed = Math.Min(8, Math.Max(1, cvSkills.Count));
        var comps = new List<CompetencyItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in generated)
        {
            var mapped = CoachCvSkillGate.MapToCvSkill(c.SkillName, cvSkills);
            if (mapped is null) continue;
            if (!seen.Add(mapped)) continue;

            comps.Add(new CompetencyItem
            {
                SkillKey = string.IsNullOrWhiteSpace(c.SkillKey)
                    ? CompetencyScoringService.NormalizeSkill(mapped).Replace(' ', '-')
                    : c.SkillKey.Trim(),
                SkillName = mapped,
                Category = NormalizeCategory(c.Category),
                Weight = c.Weight,
                ExpectedLevel = targetLevel,
                TargetScore = targetScore,
                Topics = c.Topics.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList(),
                Source = CompetencySourceMode.Rag,
                Citations = (c.Citations ?? []).Select(x => new CompetencyCitation
                {
                    SourceTitle = x.SourceTitle,
                    SourceUrl = x.SourceUrl,
                    Section = x.Section,
                    DocumentId = x.DocumentId,
                    Excerpt = x.Excerpt
                }).ToList()
            });

            if (comps.Count >= maxAllowed) break;
        }

        foreach (var item in comps.Where(i => i.Topics.Count == 0))
            item.Topics = [$"{item.SkillName} fundamentals", $"{item.SkillName} application"];

        return comps;
    }

    public static List<CompetencyItem> FallbackFromCv(
        IReadOnlyList<string> cvSkills,
        string targetLevel,
        double targetScore)
    {
        var take = Math.Min(8, Math.Max(1, cvSkills.Count));
        var selected = cvSkills.Take(take).ToList();
        var weight = Math.Round(1.0 / selected.Count, 4);
        var comps = selected.Select(skill => new CompetencyItem
        {
            SkillKey = CompetencyScoringService.NormalizeSkill(skill).Replace(' ', '-'),
            SkillName = skill,
            Category = CompetencyCategory.RoleCore,
            Weight = weight,
            ExpectedLevel = targetLevel,
            TargetScore = targetScore,
            Topics = [$"{skill} fundamentals", $"{skill} application"],
            Source = CompetencySourceMode.Rag,
            Citations = []
        }).ToList();
        RenormalizeWeights(comps);
        return comps;
    }

    public static void RenormalizeWeights(List<CompetencyItem> comps)
    {
        if (comps.Count == 0) return;
        var sum = comps.Sum(c => c.Weight);
        if (sum <= 0)
        {
            var even = Math.Round(1.0 / comps.Count, 4);
            foreach (var c in comps) c.Weight = even;
            comps[^1].Weight = Math.Round(1.0 - even * (comps.Count - 1), 4);
            return;
        }

        if (Math.Abs(sum - 1.0) <= 0.01) return;
        foreach (var c in comps)
            c.Weight = Math.Round(c.Weight / sum, 4);
        var drift = Math.Round(1.0 - comps.Sum(c => c.Weight), 4);
        comps[^1].Weight = Math.Round(comps[^1].Weight + drift, 4);
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

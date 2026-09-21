using System.Text.RegularExpressions;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Services.Coach;

namespace ApplicationLayer.Helpers;

/// <summary>
/// SCRUM-461: cổng cuối Adaptive — skill ⊆ CV; drop chunk lệch stack.
/// Không hardcode FE/.NET: stack = tập skill CV (normalize + contains).
/// </summary>
public static class CoachCvSkillGate
{
    /// <summary>Giữ chunk nếu ít nhất 1 skill CV khớp content/title/section.</summary>
    public static List<RagCompetencyChunkDto> FilterChunks(
        IEnumerable<RagCompetencyChunkDto>? chunks,
        IReadOnlyList<string> cvSkills)
    {
        var list = chunks?.ToList() ?? new List<RagCompetencyChunkDto>();
        if (cvSkills.Count == 0 || list.Count == 0)
            return list;

        return list.Where(c => ChunkOverlapsSkills(c, cvSkills)).ToList();
    }

    public static bool ChunkOverlapsSkills(RagCompetencyChunkDto chunk, IReadOnlyList<string> skills)
    {
        var haystack = $"{chunk.Content} {chunk.SourceTitle} {chunk.Section}";
        return skills.Any(s => SkillInHaystack(s, haystack));
    }

    /// <summary>Map tên LLM về skill CV; null nếu không thuộc CV.</summary>
    public static string? MapToCvSkill(string? raw, IReadOnlyList<string> cvSkills)
        => BlueprintComplianceValidator.MapSkill(raw, cvSkills);

    public static string NormalizeSkillToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == '#' || c == '+' || c == '.')
            .ToArray();
        return new string(chars);
    }

    private static bool SkillInHaystack(string? skill, string haystack)
    {
        var skillNorm = NormalizeSkillToken(skill);
        if (skillNorm.Length == 0 || string.IsNullOrWhiteSpace(haystack))
            return false;

        var hayLower = haystack.ToLowerInvariant();
        var hayNorm = NormalizeSkillToken(haystack);
        if (hayNorm.Length == 0) return false;

        // Skill ngắn (Go, C#): khớp token — tránh "going".
        if (skillNorm.Length <= 3)
        {
            foreach (Match m in Regex.Matches(hayLower, @"[a-z0-9#+.]+"))
            {
                if (NormalizeSkillToken(m.Value) == skillNorm)
                    return true;
            }
            return false;
        }

        return hayNorm.Contains(skillNorm, StringComparison.Ordinal)
               || skillNorm.Contains(hayNorm, StringComparison.Ordinal);
    }
}

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// Chuẩn hóa list tech skills HR chỉnh trên Studio trước khi ghi DetectedSkillsJson.
/// </summary>
public static class StudioHrSkillsHelper
{
    public const int MaxSkills = 20;
    public const int MaxSkillLength = 80;

    /// <summary>Trim, bỏ rỗng, dedupe (case-insensitive), tối đa MaxSkills.</summary>
    public static string[] Normalize(IReadOnlyList<string>? skills)
    {
        if (skills is null || skills.Count == 0) return [];

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(Math.Min(skills.Count, MaxSkills));
        foreach (var raw in skills)
        {
            if (result.Count >= MaxSkills) break;
            var s = (raw ?? string.Empty).Trim();
            if (s.Length == 0) continue;
            if (s.Length > MaxSkillLength) s = s[..MaxSkillLength];
            if (!seen.Add(s)) continue;
            result.Add(s);
        }
        return result.ToArray();
    }
}

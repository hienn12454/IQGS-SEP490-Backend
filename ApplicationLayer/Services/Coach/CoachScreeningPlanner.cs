using ApplicationLayer.Helpers;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-506: chọn skill CV chưa đo cho bài sàng lọc — deterministic, không LLM.
/// Loại skill đã có điểm diagnostic và skill đã có roadmap; xếp theo ImportanceWeight.
/// </summary>
public static class CoachScreeningPlanner
{
    public static List<string> SelectSkills(
        IReadOnlyList<string> cvSkills,
        IEnumerable<string> alreadyMeasured,
        IEnumerable<string> existingRoadmapSkills,
        CompetencyFramework? framework,
        int maxSkills)
    {
        var cap = Math.Clamp(maxSkills, 1, 20);
        var measured = ToNormSet(alreadyMeasured);
        var onRoadmap = ToNormSet(existingRoadmapSkills);

        var candidates = new List<(string Name, double Weight, int Order)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var order = 0;
        foreach (var raw in cvSkills ?? Array.Empty<string>())
        {
            var name = (raw ?? string.Empty).Trim();
            if (name.Length == 0) continue;
            var key = CompetencyScoringService.NormalizeSkill(name);
            if (key.Length == 0 || !seen.Add(key)) continue;
            if (measured.Contains(key) || onRoadmap.Contains(key)) continue;

            var fw = framework?.Skills.FirstOrDefault(s =>
                CompetencyScoringService.NormalizeSkill(s.Skill) == key);
            candidates.Add((name, fw?.ImportanceWeight ?? 0, order++));
        }

        return candidates
            .OrderByDescending(c => c.Weight)
            .ThenBy(c => c.Order)
            .Take(cap)
            .Select(c => c.Name)
            .ToList();
    }

    private static HashSet<string> ToNormSet(IEnumerable<string>? values)
        => (values ?? Array.Empty<string>())
            .Select(CompetencyScoringService.NormalizeSkill)
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
}

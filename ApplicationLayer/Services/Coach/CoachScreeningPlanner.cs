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
        var cap = Math.Clamp(maxSkills, 1, 30);
        var measuredList = alreadyMeasured?.ToList() ?? new List<string>();
        var roadmapList = existingRoadmapSkills?.ToList() ?? new List<string>();
        var measured = ToNormSet(measuredList);
        var onRoadmap = ToNormSet(roadmapList);

        // Giữ tên gốc để so biến thể: bài chẩn đoán đã gộp "ASP.NET" vào "ASP.NET Core",
        // "SQL Server" vào "SQL" → không được mời sàng lọc lại những skill đó.
        var coveredNames = measuredList
            .Concat(roadmapList)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

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
            if (CoachQuickCheckSkills.IsCoveredBy(name, coveredNames)) continue;

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

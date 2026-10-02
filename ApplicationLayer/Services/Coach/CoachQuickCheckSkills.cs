using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.Helpers;
using ApplicationLayer.Studio.Helpers;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// Đo nhanh mọi skill trên CV nằm ngoài nhóm core của bài chẩn đoán.
/// Yêu cầu nghiệp vụ: sau bài chẩn đoán, lộ trình phải có đủ mọi skill ứng viên đã xác nhận ở bước
/// Phân tích CV. Nhóm core vẫn hỏi sâu (3 câu/skill) để tính level; skill ngoài core chỉ hỏi nhanh
/// để biết có yếu hay không, rồi đưa vào lộ trình.
/// </summary>
public static class CoachQuickCheckSkills
{
    /// <summary>
    /// Trọng số chỉ dùng để xếp ưu tiên lộ trình: nhỏ hơn skill core nên luôn đứng sau,
    /// nhưng khác 0 để skill yếu vẫn có mức ưu tiên thay vì bị gắn "thấp".
    /// </summary>
    public const double RoadmapWeight = 0.05;

    /// <summary>Selected = skill sẽ hỏi nhanh; Overflow = vượt trần Admin, để bài sàng lọc xử lý sau.</summary>
    public sealed record Selection(List<string> Selected, List<string> Overflow);

    /// <summary>
    /// Chọn skill CV cần đo nhanh: bỏ skill đã có trong core (kể cả biến thể như "SQL Server" ~ "SQL"),
    /// gộp tên trùng nhau, giữ đúng thứ tự trên CV, cắt theo trần.
    /// </summary>
    public static Selection Select(
        IReadOnlyList<string>? cvSkills,
        IReadOnlyList<string> coreSkills,
        int maxSkills)
    {
        var selected = new List<string>();
        var overflow = new List<string>();

        // "known" = core + skill đã chọn, để skill sau trùng/biến thể của skill trước thì bỏ qua.
        var known = coreSkills
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        foreach (var raw in cvSkills ?? Array.Empty<string>())
        {
            var name = (raw ?? string.Empty).Trim();
            if (name.Length == 0) continue;
            if (IsCoveredBy(name, known)) continue;

            known.Add(name);
            if (selected.Count < maxSkills)
                selected.Add(name);
            else
                overflow.Add(name);
        }

        return new Selection(selected, overflow);
    }

    /// <summary>
    /// Skill có trùng hoặc là biến thể của một tên trong danh sách không.
    /// So theo token (TechSkillMatcher) để "Java" không bị gộp vào "JavaScript",
    /// rồi thử thêm tên chuẩn của catalog ("RESTful APIs" → "REST API").
    /// </summary>
    public static bool IsCoveredBy(string skill, IReadOnlyList<string> names)
    {
        if (string.IsNullOrWhiteSpace(skill) || names.Count == 0) return false;
        if (TechSkillMatcher.MapToAllowed(skill, names) is not null) return true;

        var canonical = TechSkillCatalog.TryMap(skill);
        return canonical is not null
               && TechSkillMatcher.MapToAllowed(canonical, names) is not null;
    }

    /// <summary>
    /// Dựng competency đo nhanh. Target theo policy của level; nếu skill có trong framework
    /// (nhưng không lọt nhóm core vì trần Admin) thì dùng target/topic của framework.
    /// </summary>
    public static List<CompetencyItem> BuildCompetencies(
        IReadOnlyList<string> skills,
        string targetLevel,
        double defaultTargetScore,
        CompetencyFramework? framework)
    {
        var result = new List<CompetencyItem>();
        foreach (var skill in skills)
        {
            var key = CompetencyScoringService.NormalizeSkill(skill);
            var fwSkill = framework?.Skills.FirstOrDefault(s =>
                CompetencyScoringService.NormalizeSkill(s.Skill) == key);
            var fwTopics = CompetencyTopicParser.ParseTopicNames(fwSkill?.TopicsJson);

            result.Add(new CompetencyItem
            {
                SkillKey = key.Replace(' ', '-'),
                SkillName = skill,
                // Ngoài framework: hỏi câu nền tảng (easy) — 1 câu nền tảng đủ để biết có hổng kiến thức không.
                Category = fwSkill is null
                    ? CompetencyCategory.Fundamental
                    : FrameworkBlueprintBuilder.CategoryFromDifficulty(fwSkill.RequiredDifficulty),
                Weight = RoadmapWeight,
                ExpectedLevel = targetLevel,
                TargetScore = fwSkill is { TargetScore: > 0 } ? fwSkill.TargetScore : defaultTargetScore,
                Topics = fwTopics.Count > 0 ? fwTopics : [$"{skill} fundamentals"],
                Source = fwSkill is null ? CompetencySourceMode.Rag : CompetencySourceMode.Framework,
                QuickCheck = true
            });
        }
        return result;
    }

    /// <summary>Tập tên skill đo nhanh (đã normalize) của blueprint; rỗng nếu không có.</summary>
    public static HashSet<string> QuickCheckKeys(CompetencyBlueprint? blueprint)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (blueprint is null) return keys;

        foreach (var item in blueprint.Competencies.Where(c => c.QuickCheck))
        {
            var key = CompetencyScoringService.NormalizeSkill(item.SkillName);
            if (key.Length > 0) keys.Add(key);
        }
        return keys;
    }
}

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// HG01: gom các thao tác trên 1 slot outline (recommendedQuestionOutline) vào một chỗ.
/// Nguyên tắc: skill, goal (Why ask) và citations của 1 slot phải cùng nói về MỘT chủ đề.
/// Muốn đổi skill thì đổi cả slot — không được chỉ thay nhãn như bản SCRUM-435 cũ,
/// vì RAG viết câu hỏi theo goal + nguồn còn badge Domain lại lấy theo skill.
/// </summary>
public static class StudioOutlineSlotHelper
{
    /// <summary>Skill mà goal/citations hiện tại đang mô tả. Khác skill → slot "lai" cần sửa.</summary>
    public const string PlannedSkillKey = "plannedSkill";

    /// <summary>Slot đã bị đổi skill (theo % focus hoặc HR đổi) → goal viết lại, nguồn chờ gắn lại.</summary>
    public const string RelabeledKey = "relabeled";

    // Chỉ slot kỹ thuật mới chia theo % focus. Behavioral/situational giữ nhãn mềm,
    // nếu không sẽ ra "câu hành vi gắn Domain HTML" (HG01 câu 10).
    private static readonly HashSet<string> TechnicalTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "technical", "technicalfoundations", "problemsolving", "systemdesign", "coding", "algorithm"
    };

    public static bool IsTechnicalType(string? type)
    {
        var key = Regex.Replace(type ?? "", @"[\s_\-]", "");
        return TechnicalTypes.Contains(key);
    }

    public static bool IsTechnicalSlot(JsonObject slot)
    {
        return IsTechnicalType(ReadString(slot, "type"));
    }

    /// <summary>"React.js" / "ReactJS" / "react" → "react"; "ASP.NET Core" → "aspnetcore".</summary>
    public static string CanonSkill(string? name)
    {
        var compact = Regex.Replace((name ?? "").ToLowerInvariant(), @"[\s._\-]", "");
        return compact.Length > 4 && compact.EndsWith("js") ? compact[..^2] : compact;
    }

    public static bool SameSkill(string? a, string? b)
    {
        return CanonSkill(a) == CanonSkill(b);
    }

    /// <summary>Why ask mặc định khi slot đổi skill — viết theo ngôn ngữ đầu ra của bộ câu hỏi.</summary>
    public static string BuildDefaultGoal(string skill, string? outputLanguage)
    {
        return StudioOutputLanguage.Normalize(outputLanguage) == StudioOutputLanguage.English
            ? $"Assess hands-on {skill} knowledge relevant to this role."
            : $"Đánh giá kiến thức {skill} thực tế cho vị trí này.";
    }

    /// <summary>Đọc string an toàn — LLM đôi khi trả số/null thay vì string.</summary>
    public static string ReadString(JsonObject slot, string key)
    {
        return slot[key] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text.Trim()
            : "";
    }

    public static string ReadSkill(JsonObject slot)
    {
        var skill = ReadString(slot, "skill");
        if (skill.Length > 0)
            return skill;

        skill = ReadString(slot, "focusArea");
        return skill.Length > 0 ? skill : ReadString(slot, "focus_area");
    }

    /// <summary>Chỉ đổi cách viết tên (vd "React" → "React.js") — chủ đề không đổi nên goal giữ nguyên.</summary>
    public static void RenameSkill(JsonObject slot, string skill)
    {
        slot["skill"] = skill;
        slot["focusArea"] = skill;
        slot["focus_area"] = skill;
        slot[PlannedSkillKey] = skill;
    }

    /// <summary>
    /// Đổi CẢ slot sang skill mới: goal viết lại, citations cũ bỏ (RAG gắn lại JD/SYSTEM theo skill mới),
    /// đánh dấu relabeled để Live Preview báo cho HR biết slot này vừa được đổi.
    /// </summary>
    public static void RelabelWholeSlot(JsonObject slot, string newSkill, string? outputLanguage)
    {
        RenameSkill(slot, newSkill);
        slot["goal"] = BuildDefaultGoal(newSkill, outputLanguage);
        slot["citations"] = new JsonArray();
        slot[RelabeledKey] = true;
    }

    /// <summary>Đóng dấu: goal/citations hiện tại đang mô tả đúng skill hiện tại của slot.</summary>
    public static void MarkCoherent(JsonObject slot)
    {
        slot[PlannedSkillKey] = ReadSkill(slot);
    }

    public static JsonObject UnwrapPlan(JsonObject root)
    {
        return root["plan"] as JsonObject ?? root;
    }

    public static JsonArray? GetOutline(JsonObject planObj)
    {
        return planObj["recommendedQuestionOutline"] as JsonArray
            ?? planObj["recommended_question_outline"] as JsonArray;
    }

    /// <summary>Ghi outline vào cả 2 key (RAG đọc camelCase, dữ liệu cũ còn snake_case).</summary>
    public static void WriteOutline(JsonObject planObj, JsonArray outline)
    {
        var finalOutline = outline.DeepClone();
        planObj["recommendedQuestionOutline"] = finalOutline;
        planObj["recommended_question_outline"] = finalOutline.DeepClone();
    }

    /// <summary>
    /// Coverage (chip "C# · 5") phải bằng đúng số slot trong outline. Nếu lệch, LLM nhận 2 chỉ dẫn
    /// mâu thuẫn (coverage nói 7 câu C#, outline chỉ có 5 slot C#) và dễ viết lệch skill.
    /// </summary>
    public static void SyncCoverageCounts(JsonObject planObj, JsonArray outline)
    {
        if (planObj["coverage"] is not JsonArray coverage || coverage.Count == 0)
            return;

        var slotCountBySkill = outline.OfType<JsonObject>()
            .GroupBy(slot => CanonSkill(ReadSkill(slot)))
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var item in coverage.OfType<JsonObject>())
        {
            var skill = ReadString(item, "skill");
            var count = slotCountBySkill.GetValueOrDefault(CanonSkill(skill));
            item["questionCount"] = count;
            item["question_count"] = count;
        }
    }
}

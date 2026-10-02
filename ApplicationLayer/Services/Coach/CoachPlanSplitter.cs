using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// Tách PlanJson bài chẩn đoán thành nhiều phần để sinh đề song song:
/// phần core (các dòng hỏi sâu) + các nhóm câu đo nhanh (tối đa <see cref="DefaultChunkSize"/> câu/nhóm).
/// Lý do: có thêm câu đo nhanh cho mọi skill CV thì đề dài gần gấp đôi; sinh 1 lần dễ vượt
/// timeout 5 phút của RAG. Chia nhỏ + chạy song song giữ thời gian chờ gần như cũ.
/// </summary>
public static class CoachPlanSplitter
{
    public const int DefaultChunkSize = 10;

    /// <summary>Một phần plan gửi RAG: Plan là JSON đã đánh số lại order từ 1.</summary>
    public sealed record PlanPart(JsonElement Plan, List<string> Skills, int TotalQuestions);

    /// <summary>
    /// Trả về danh sách phần plan. Plan không có câu đo nhanh → danh sách rỗng
    /// (caller giữ nguyên cách gọi RAG 1 lần như trước).
    /// </summary>
    public static List<PlanPart> SplitQuickCheck(string? planJson, int chunkSize = DefaultChunkSize)
    {
        var parts = new List<PlanPart>();
        if (string.IsNullOrWhiteSpace(planJson)) return parts;

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(planJson) as JsonObject;
        }
        catch (JsonException)
        {
            return parts;
        }
        if (root?["recommendedQuestionOutline"] is not JsonArray outline) return parts;

        var rows = outline.OfType<JsonObject>().ToList();
        var quickRows = rows.Where(IsQuickCheckRow).ToList();
        if (quickRows.Count == 0) return parts;

        var coreRows = rows.Where(r => !IsQuickCheckRow(r)).ToList();
        if (coreRows.Count > 0)
            parts.Add(BuildPart(root, coreRows));

        var size = Math.Max(1, chunkSize);
        for (var start = 0; start < quickRows.Count; start += size)
            parts.Add(BuildPart(root, quickRows.Skip(start).Take(size).ToList()));

        return parts;
    }

    public static bool IsQuickCheckRow(JsonObject row)
        => row["quickCheck"] is JsonValue value
           && value.TryGetValue<bool>(out var isQuickCheck)
           && isQuickCheck;

    private static PlanPart BuildPart(JsonObject root, List<JsonObject> rows)
    {
        var part = (JsonObject)root.DeepClone();

        // Đánh số lại từ 1 trong từng phần; thứ tự thật được gán lại theo slot khi validate cả đề.
        var outline = new JsonArray();
        var order = 1;
        foreach (var row in rows)
        {
            var copy = (JsonObject)row.DeepClone();
            copy["order"] = order++;
            outline.Add(copy);
        }

        var skills = rows
            .Select(r => ReadString(r, "skill"))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var skillArray = new JsonArray();
        foreach (var skill in skills)
            skillArray.Add(skill);

        var difficultyArray = new JsonArray();
        foreach (var group in rows.GroupBy(r => ReadString(r, "difficulty") ?? "medium"))
            difficultyArray.Add(new JsonObject { ["difficulty"] = group.Key, ["count"] = group.Count() });

        part["recommendedQuestionOutline"] = outline;
        part["totalQuestions"] = rows.Count;
        part["skills"] = skillArray;
        part["difficultyDistribution"] = difficultyArray;
        part["questionTypeDistribution"] = new JsonArray(new JsonObject
        {
            ["type"] = "technical",
            ["count"] = rows.Count,
            ["reason"] = "Competency evidence"
        });

        return new PlanPart(JsonSerializer.SerializeToElement(part), skills, rows.Count);
    }

    private static string? ReadString(JsonObject row, string property)
        => row[property] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}

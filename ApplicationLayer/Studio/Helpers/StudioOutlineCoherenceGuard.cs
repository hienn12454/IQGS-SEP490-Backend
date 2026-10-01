using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// HG01 chốt chặn: chạy trước khi lưu plan và trước khi gửi RAG sinh câu hỏi.
/// Slot nào có skill ≠ plannedSkill (skill bị đổi ở đâu đó mà goal/nguồn chưa đổi theo)
/// → đổi CẢ slot theo skill hiện tại. Slot chưa có plannedSkill (plan cũ, slot RAG vừa refine)
/// → coi như LLM/HR viết cả cụm nên nhất quán, chỉ đóng dấu.
/// </summary>
public static class StudioOutlineCoherenceGuard
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Apply(string? sourcePlanJson, string? outputLanguage, out int fixedSlots)
    {
        fixedSlots = 0;
        if (string.IsNullOrWhiteSpace(sourcePlanJson))
            return sourcePlanJson ?? "";

        JsonObject root;
        try
        {
            if (JsonNode.Parse(sourcePlanJson) is not JsonObject parsed)
                return sourcePlanJson;
            root = parsed;
        }
        catch
        {
            // JSON hỏng thì để luồng cũ báo lỗi như trước, guard không tự nuốt
            return sourcePlanJson;
        }

        var planObj = StudioOutlineSlotHelper.UnwrapPlan(root);
        var outline = StudioOutlineSlotHelper.GetOutline(planObj);
        if (outline is null || outline.Count == 0)
            return sourcePlanJson;

        foreach (var slot in outline.OfType<JsonObject>())
        {
            if (FixSlotIfStale(slot, outputLanguage))
                fixedSlots++;
        }

        StudioOutlineSlotHelper.SyncCoverageCounts(planObj, outline);
        StudioOutlineSlotHelper.WriteOutline(planObj, outline);
        return root.ToJsonString(JsonOptions);
    }

    private static bool FixSlotIfStale(JsonObject slot, string? outputLanguage)
    {
        var skill = StudioOutlineSlotHelper.ReadSkill(slot);
        var plannedSkill = StudioOutlineSlotHelper.ReadString(slot, StudioOutlineSlotHelper.PlannedSkillKey);

        if (plannedSkill.Length == 0)
        {
            StudioOutlineSlotHelper.MarkCoherent(slot);
            return false;
        }

        if (skill.Length == 0 || StudioOutlineSlotHelper.SameSkill(skill, plannedSkill))
            return false;

        StudioOutlineSlotHelper.RelabelWholeSlot(slot, skill, outputLanguage);
        return true;
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// SCRUM-435 (+ HG01): chia slot kỹ thuật theo % FocusAreas của HR (không gọi RAG).
/// Bản cũ gán skill theo VỊ TRÍ trong hàng đợi % focus nhưng giữ nguyên goal/citations của LLM
/// → slot "lai" (vd Domain React.js nhưng Why ask + nguồn nói Node.js/.NET) → câu hỏi lệch nhãn.
/// Bản này: slot nào có skill gốc còn quota thì giữ nguyên cả slot; slot buộc phải đổi thì đổi
/// CẢ skill + goal + citations. Slot behavioral/situational không nhận skill kỹ thuật.
/// </summary>
public static class StudioOutlineFocusRedistributor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed class FocusQuota
    {
        public string Name { get; init; } = "";
        public int Remaining { get; set; }
    }

    public static StudioRagPlanMapper.MappedPlan ApplyFocusWeightsToOutline(
        StudioRagPlanMapper.MappedPlan mapped,
        string? outputLanguage = null)
    {
        if (mapped.FocusAreas is not { Count: > 0 })
            return mapped;
        if (string.IsNullOrWhiteSpace(mapped.SourcePlanJson))
            return mapped;

        try
        {
            if (JsonNode.Parse(mapped.SourcePlanJson) is not JsonObject root)
                return mapped;

            var planObj = StudioOutlineSlotHelper.UnwrapPlan(root);
            var outline = StudioOutlineSlotHelper.GetOutline(planObj);
            if (outline is null || outline.Count == 0)
                return mapped;

            var technicalSlots = outline.OfType<JsonObject>()
                .Where(StudioOutlineSlotHelper.IsTechnicalSlot)
                .ToList();
            var quotas = BuildQuotas(mapped.FocusAreas, technicalSlots);
            var slotsToRelabel = KeepSlotsWithQuota(technicalSlots, quotas);
            RelabelWholeSlots(slotsToRelabel, quotas, outputLanguage);

            for (var i = 0; i < outline.Count; i++)
            {
                if (outline[i] is not JsonObject slot)
                    continue;
                slot["order"] = i + 1;
                // Slot không bị đổi (kể cả behavioral) → goal/nguồn hiện tại khớp skill hiện tại
                if (slot[StudioOutlineSlotHelper.PlannedSkillKey] is null)
                    StudioOutlineSlotHelper.MarkCoherent(slot);
            }

            StudioOutlineSlotHelper.SyncCoverageCounts(planObj, outline);
            StudioOutlineSlotHelper.WriteOutline(planObj, outline);
            return mapped with { SourcePlanJson = root.ToJsonString(JsonOptions) };
        }
        catch
        {
            return mapped;
        }
    }

    /// <summary>Có slot nào vừa bị đổi skill (goal viết lại, citations đã xoá) cần gắn lại nguồn không.</summary>
    public static bool HasRelabeledSlots(string? sourcePlanJson)
    {
        if (string.IsNullOrWhiteSpace(sourcePlanJson))
            return false;
        try
        {
            if (JsonNode.Parse(sourcePlanJson) is not JsonObject root)
                return false;
            var outline = StudioOutlineSlotHelper.GetOutline(StudioOutlineSlotHelper.UnwrapPlan(root));
            return outline is not null && outline.OfType<JsonObject>().Any(slot =>
                slot[StudioOutlineSlotHelper.RelabeledKey] is JsonValue flag
                && flag.TryGetValue<bool>(out var relabeled)
                && relabeled);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Quota = % focus × số slot KỸ THUẬT (không phải tổng số câu).
    /// Khi 2 skill cùng %, ưu tiên skill LLM đã lên slot để phải đổi ít slot nhất
    /// (LargestRemainder hoà điểm thì lấy theo thứ tự trong list).
    /// </summary>
    private static List<FocusQuota> BuildQuotas(
        IReadOnlyList<StudioRagPlanMapper.PlanFocusAreaDraft> focusAreas,
        List<JsonObject> technicalSlots)
    {
        var plannedSkills = technicalSlots
            .Select(slot => StudioOutlineSlotHelper.CanonSkill(StudioOutlineSlotHelper.ReadSkill(slot)))
            .ToHashSet();

        var orderedFocus = focusAreas
            .Where(f => !string.IsNullOrWhiteSpace(f.Name))
            .OrderByDescending(f => plannedSkills.Contains(StudioOutlineSlotHelper.CanonSkill(f.Name)))
            .ThenBy(f => f.OrderIndex)
            .ToList();

        var weights = orderedFocus
            .Select(f => (int)Math.Round(StudioFocusAreaWeightHelper.NormalizeToPercent(f.Weight)))
            .ToList();
        var counts = StudioProportionalAllocator.LargestRemainder(weights, technicalSlots.Count);

        var quotas = new List<FocusQuota>();
        for (var i = 0; i < orderedFocus.Count; i++)
        {
            quotas.Add(new FocusQuota { Name = orderedFocus[i].Name.Trim(), Remaining = counts[i] });
        }
        return quotas;
    }

    /// <summary>Pass 1: slot có skill gốc còn quota → giữ nguyên cả slot (goal + nguồn vẫn đúng chủ đề).</summary>
    private static List<JsonObject> KeepSlotsWithQuota(List<JsonObject> technicalSlots, List<FocusQuota> quotas)
    {
        var slotsToRelabel = new List<JsonObject>();
        foreach (var slot in technicalSlots)
        {
            var slotSkill = StudioOutlineSlotHelper.ReadSkill(slot);
            var quota = quotas.FirstOrDefault(q =>
                q.Remaining > 0 && StudioOutlineSlotHelper.SameSkill(q.Name, slotSkill));
            if (quota is null)
            {
                slotsToRelabel.Add(slot);
                continue;
            }

            quota.Remaining--;
            // Chuẩn hoá tên theo HR ("React" → "React.js") để badge Domain thống nhất
            StudioOutlineSlotHelper.RenameSkill(slot, quota.Name);
        }
        return slotsToRelabel;
    }

    /// <summary>
    /// Pass 2: slot còn lại nhận skill đang thiếu quota. Đổi CẢ slot: goal viết lại theo skill mới
    /// và xoá citations cũ — BE gọi bind-outline-sources / RAG tự gắn JD theo skill mới.
    /// </summary>
    private static void RelabelWholeSlots(List<JsonObject> slots, List<FocusQuota> quotas, string? outputLanguage)
    {
        var missingSkills = quotas
            .SelectMany(q => Enumerable.Repeat(q.Name, q.Remaining))
            .ToList();

        for (var i = 0; i < slots.Count && i < missingSkills.Count; i++)
        {
            StudioOutlineSlotHelper.RelabelWholeSlot(slots[i], missingSkills[i], outputLanguage);
        }
    }
}

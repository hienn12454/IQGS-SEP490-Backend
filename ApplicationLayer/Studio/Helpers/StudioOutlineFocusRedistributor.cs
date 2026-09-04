using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// SCRUM-435: Gán lại skill/focusArea trên outline theo trọng số FocusAreas (không gọi RAG).
/// Giữ type/difficulty/goal/answerMethod/citations; chỉ đổi nhãn skill trên slot.
/// </summary>
public static class StudioOutlineFocusRedistributor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static StudioRagPlanMapper.MappedPlan ApplyFocusWeightsToOutline(
        StudioRagPlanMapper.MappedPlan mapped)
    {
        if (mapped.FocusAreas is not { Count: > 0 })
            return mapped;
        if (string.IsNullOrWhiteSpace(mapped.SourcePlanJson))
            return mapped;

        try
        {
            var node = JsonNode.Parse(mapped.SourcePlanJson);
            if (node is not JsonObject root)
                return mapped;

            var planObj = root;
            if (root["plan"] is JsonObject nested)
                planObj = nested;

            JsonArray? outline = planObj["recommendedQuestionOutline"] as JsonArray
                ?? planObj["recommended_question_outline"] as JsonArray;
            if (outline is null || outline.Count == 0)
                return mapped;

            var items = mapped.FocusAreas
                .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                .OrderBy(f => f.OrderIndex)
                .Select(f => (
                    Name: f.Name.Trim(),
                    Weight: (int)Math.Round(StudioFocusAreaWeightHelper.NormalizeToPercent(f.Weight))))
                .ToList();
            if (items.Count == 0)
                return mapped;

            var queue = StudioProportionalAllocator.BuildNameQueue(items, outline.Count);
            if (queue.Count == 0)
                return mapped;

            for (var i = 0; i < outline.Count; i++)
            {
                if (outline[i] is not JsonObject slot) continue;
                var skill = i < queue.Count ? queue[i] : queue[^1];
                slot["skill"] = skill;
                slot["focusArea"] = skill;
                slot["focus_area"] = skill;
                // Cập nhật goal mặc định nếu chưa có / đang generic
                var goal = slot["goal"]?.GetValue<string>()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(goal)
                    || goal.StartsWith("Đánh giá năng lực", StringComparison.OrdinalIgnoreCase)
                    || goal.StartsWith("Đánh giá ", StringComparison.OrdinalIgnoreCase)
                       && goal.Contains("trong ngữ cảnh", StringComparison.OrdinalIgnoreCase))
                {
                    slot["goal"] = $"Đánh giá {skill} trong ngữ cảnh vị trí.";
                }
                slot["order"] = i + 1;
            }

            planObj["recommendedQuestionOutline"] = outline;
            planObj["recommended_question_outline"] = outline.DeepClone();

            return mapped with { SourcePlanJson = root.ToJsonString(JsonOptions) };
        }
        catch
        {
            return mapped;
        }
    }
}

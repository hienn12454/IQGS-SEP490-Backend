using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>SCRUM-420: Merge baseline SourcePlanJson + PlanPatch delta → plan JSON đầy đủ.</summary>
public static class PlanMergeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Merge baseline + patch RAG; trả MappedPlan cho persist in-place.</summary>
    public static StudioRagPlanMapper.MappedPlan Merge(
        string baselineSourcePlanJson,
        object? patchObj,
        int fallbackQuestionCount,
        int? preferredMinutes)
    {
        if (string.IsNullOrWhiteSpace(baselineSourcePlanJson))
            throw new InvalidOperationException("Baseline SourcePlanJson trống.");

        var baselineNode = JsonNode.Parse(baselineSourcePlanJson)
            ?? throw new InvalidOperationException("Baseline JSON không hợp lệ.");

        if (baselineNode is JsonObject root && root.ContainsKey("plan") && root["plan"] is JsonObject nested)
            baselineNode = nested;

        var patchJson = patchObj switch
        {
            JsonElement el => el.GetRawText(),
            JsonNode node => node.ToJsonString(),
            string s => s,
            _ => JsonSerializer.Serialize(patchObj, JsonOptions)
        };

        using var patchDoc = JsonDocument.Parse(patchJson);
        ApplyPatch(baselineNode as JsonObject ?? throw new InvalidOperationException("Baseline không phải object."), patchDoc.RootElement);

        var mergedJson = baselineNode.ToJsonString();
        using var mergedDoc = JsonDocument.Parse(mergedJson);
        var validated = PlanProvenanceValidator.Apply(mergedDoc.RootElement);
        var canonical = validated.GetRawText();

        return StudioRagPlanMapper.MapFromRagPlanObject(canonical, fallbackQuestionCount, preferredMinutes);
    }

    private static void ApplyPatch(JsonObject baseline, JsonElement patch)
    {
        if (TryGetPatchArray(patch, out var coverage, "replaceCoverage", "replace_coverage"))
            baseline["coverage"] = JsonNode.Parse(coverage.GetRawText());

        if (TryGetPatchArray(patch, out var skills, "replaceSkills", "replace_skills"))
        {
            var skillList = new JsonArray();
            foreach (var item in skills.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    skillList.Add(item.GetString());
                else if (item.ValueKind == JsonValueKind.Object
                         && item.TryGetProperty("name", out var nameProp))
                    skillList.Add(nameProp.GetString());
            }
            baseline["skills"] = skillList;
        }

        if (TryGetPatchArray(patch, out var outline, "replaceOutline", "replace_outline", "appendOutline", "append_outline"))
            baseline["recommendedQuestionOutline"] = JsonNode.Parse(outline.GetRawText());

        if (TryGetPatchArray(patch, out var qtd, "replaceQuestionTypeDistribution", "replace_question_type_distribution"))
            baseline["questionTypeDistribution"] = JsonNode.Parse(qtd.GetRawText());

        if (TryGetPatchArray(patch, out var dd, "replaceDifficultyDistribution", "replace_difficulty_distribution"))
            baseline["difficultyDistribution"] = JsonNode.Parse(dd.GetRawText());

        if (TryGetPatchString(patch, out var summary, "updateSummary", "update_summary"))
            baseline["summary"] = JsonValue.Create(summary);

        if (TryGetPatchArray(patch, out var citations, "replaceCitations", "replace_citations"))
            baseline["citations"] = JsonNode.Parse(citations.GetRawText());

        if (TryGetPatchInt(patch, out var total, "replaceTotalQuestions", "replace_total_questions", "totalQuestions", "total_questions"))
            baseline["totalQuestions"] = total;
    }

    private static bool TryGetPatchArray(JsonElement patch, out JsonElement array, params string[] names)
    {
        foreach (var name in names)
        {
            if (patch.TryGetProperty(name, out array) && array.ValueKind == JsonValueKind.Array && array.GetArrayLength() > 0)
                return true;
        }
        array = default;
        return false;
    }

    private static bool TryGetPatchString(JsonElement patch, out string value, params string[] names)
    {
        foreach (var name in names)
        {
            if (patch.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                value = prop.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(value)) return true;
            }
        }
        value = "";
        return false;
    }

    private static bool TryGetPatchInt(JsonElement patch, out int value, params string[] names)
    {
        foreach (var name in names)
        {
            if (!patch.TryGetProperty(name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value)) return true;
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value)) return true;
        }
        value = 0;
        return false;
    }
}

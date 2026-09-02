using System.Text.Json;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>SCRUM-420: Waterfall provenance HR → SYSTEM → LLM trên plan JSON (best-effort BE).</summary>
public static class PlanProvenanceValidator
{
    public static JsonElement Apply(JsonElement planRoot)
    {
        var root = UnwrapPlan(planRoot);
        if (root.ValueKind != JsonValueKind.Object)
            return planRoot;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.NameEquals("coverage") && prop.Value.ValueKind == JsonValueKind.Array)
                {
                    writer.WritePropertyName("coverage");
                    writer.WriteStartArray();
                    foreach (var item in prop.Value.EnumerateArray())
                        WriteCoverageItem(writer, item);
                    writer.WriteEndArray();
                    continue;
                }

                if (prop.NameEquals("citations") && prop.Value.ValueKind == JsonValueKind.Array)
                {
                    writer.WritePropertyName("citations");
                    writer.WriteStartArray();
                    foreach (var cit in prop.Value.EnumerateArray())
                        WriteCitation(writer, cit);
                    writer.WriteEndArray();
                    continue;
                }

                if (prop.NameEquals("recommendedQuestionOutline") && prop.Value.ValueKind == JsonValueKind.Array)
                {
                    writer.WritePropertyName("recommendedQuestionOutline");
                    writer.WriteStartArray();
                    foreach (var item in prop.Value.EnumerateArray())
                        WriteOutlineItem(writer, item);
                    writer.WriteEndArray();
                    continue;
                }

                prop.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }

    private static JsonElement UnwrapPlan(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("plan", out var nested)
            && nested.ValueKind == JsonValueKind.Object)
            return nested;
        return root;
    }

    private static void WriteCoverageItem(Utf8JsonWriter writer, JsonElement item)
    {
        writer.WriteStartObject();
        var skill = "";
        foreach (var p in item.EnumerateObject())
        {
            if (p.NameEquals("provenance"))
                continue;
            if (p.NameEquals("skill") && p.Value.ValueKind == JsonValueKind.String)
                skill = p.Value.GetString() ?? "";
            p.WriteTo(writer);
        }

        if (item.TryGetProperty("provenance", out var existing) && existing.ValueKind == JsonValueKind.Object)
        {
            writer.WritePropertyName("provenance");
            existing.WriteTo(writer);
        }
        else
        {
            var origin = InferOriginFromCoverage(item);
            WriteProvenanceBlock(writer, origin, skill, "coverage");
        }
        writer.WriteEndObject();
    }

    private static void WriteCitation(Utf8JsonWriter writer, JsonElement cit)
    {
        writer.WriteStartObject();
        var kb = "";
        var file = "";
        foreach (var p in cit.EnumerateObject())
        {
            if (p.NameEquals("provenance"))
                continue;
            if (p.NameEquals("knowledgeBase") || p.NameEquals("knowledge_base"))
                kb = p.Value.GetString() ?? "";
            if (p.NameEquals("sourceFile") || p.NameEquals("source_file"))
                file = p.Value.GetString() ?? "";
            p.WriteTo(writer);
        }

        if (cit.TryGetProperty("provenance", out var existing) && existing.ValueKind == JsonValueKind.Object)
        {
            writer.WritePropertyName("provenance");
            existing.WriteTo(writer);
        }
        else
        {
            var origin = InferOriginFromCitation(kb, file);
            WriteProvenanceBlock(writer, origin, file, "citation");
        }
        writer.WriteEndObject();
    }

    private static void WriteOutlineItem(Utf8JsonWriter writer, JsonElement item)
    {
        writer.WriteStartObject();
        var skill = "";
        foreach (var p in item.EnumerateObject())
        {
            if (p.NameEquals("provenance"))
                continue;
            if (p.NameEquals("skill") && p.Value.ValueKind == JsonValueKind.String)
                skill = p.Value.GetString() ?? "";
            p.WriteTo(writer);
        }

        if (item.TryGetProperty("provenance", out var existing) && existing.ValueKind == JsonValueKind.Object)
        {
            writer.WritePropertyName("provenance");
            existing.WriteTo(writer);
        }
        else
        {
            WriteProvenanceBlock(writer, "LLM", skill, "outline");
        }
        writer.WriteEndObject();
    }

    private static string InferOriginFromCoverage(JsonElement item)
    {
        if (TryGetStringArray(item, out var files, "sourceFiles", "source_files"))
        {
            foreach (var f in files)
            {
                if (IsJdFile(f)) return "HR";
            }
            foreach (var f in files)
            {
                if (!IsJdFile(f) && !string.IsNullOrWhiteSpace(f))
                    return "SYSTEM";
            }
        }
        return "LLM";
    }

    private static string InferOriginFromCitation(string knowledgeBase, string sourceFile)
    {
        if (IsJdFile(sourceFile)) return "HR";
        var kb = knowledgeBase.Trim().ToLowerInvariant();
        if (kb == "system") return "SYSTEM";
        if (kb == "hr") return "HR";
        return "LLM";
    }

    private static bool IsJdFile(string? file)
    {
        var f = (file ?? "").Trim().ToLowerInvariant();
        return f is "job-description" or "jd" or "job description" or "job_description";
    }

    private static bool TryGetStringArray(JsonElement el, out List<string> values, params string[] names)
    {
        values = [];
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var x in arr.EnumerateArray())
            {
                if (x.ValueKind == JsonValueKind.String)
                {
                    var s = x.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(s)) values.Add(s!);
                }
            }
            return values.Count > 0;
        }
        return false;
    }

    private static void WriteProvenanceBlock(Utf8JsonWriter writer, string origin, string context, string usedFor)
    {
        writer.WritePropertyName("provenance");
        writer.WriteStartObject();
        writer.WriteString("primaryOrigin", origin);
        writer.WriteStartArray("items");
        writer.WriteStartObject();
        writer.WriteString("origin", origin);
        writer.WriteStartArray("usedFor");
        writer.WriteStringValue(usedFor);
        if (!string.IsNullOrWhiteSpace(context))
            writer.WriteStringValue(context);
        writer.WriteEndArray();
        if (origin == "LLM")
            writer.WriteString("reason", "Suy luận từ instruction/JD");
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}

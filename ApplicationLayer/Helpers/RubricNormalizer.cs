using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApplicationLayer.Helpers;

/// <summary>
/// SCRUM-418: Chuẩn hóa rubric chấm điểm (RubricV1) — đồng bộ Studio, Question Builder, question_sets.
/// Hỗ trợ legacy string[] và object có weight/anchors.
/// </summary>
public static class RubricNormalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public const int RubricVersion = 1;
    public const string DefaultScale = "0-100";

    private static readonly string[] DefaultAnchorKeys = ["25", "50", "75", "100"];

    public sealed class RubricDocumentV1
    {
        public int Version { get; set; } = RubricVersion;
        public string Scale { get; set; } = DefaultScale;
        public string? Level { get; set; }
        public List<RubricCriterionV1> Criteria { get; set; } = new();
    }

    public sealed class RubricCriterionV1
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Weight { get; set; }
        public Dictionary<string, string> Anchors { get; set; } = new();
    }

    /// <summary>Parse EvaluationCriteriaJson hoặc TagsJson list — trả RubricV1.</summary>
    public static RubricDocumentV1 NormalizeFromJson(string? json, string? level = null)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
            return Empty(level);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("criteria", out var criteriaEl)
                && criteriaEl.ValueKind == JsonValueKind.Array)
            {
                return ParseDocumentObject(root, level);
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                var items = ParseArrayItems(root);
                return FromLegacyItems(items, level);
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return Empty(level);
    }

    /// <summary>Normalize từ list object/string (TagsJson / API body).</summary>
    public static RubricDocumentV1 NormalizeFromObjects(IEnumerable<object>? raw, string? level = null)
    {
        if (raw is null)
            return Empty(level);

        var items = new List<JsonElement>();
        foreach (var item in raw)
        {
            if (item is JsonElement el)
            {
                items.Add(el);
                continue;
            }

            try
            {
                var json = JsonSerializer.Serialize(item, JsonOptions);
                using var parsed = JsonDocument.Parse(json);
                items.Add(parsed.RootElement.Clone());
            }
            catch (JsonException)
            {
                // skip
            }
        }

        if (items.Count == 0)
            return Empty(level);

        if (items.Count == 1 && items[0].ValueKind == JsonValueKind.Object
            && items[0].TryGetProperty("criteria", out _))
        {
            return ParseDocumentObject(items[0], level);
        }

        return FromLegacyItems(items, level);
    }

    public static RubricDocumentV1 NormalizeFromLegacyStrings(IEnumerable<string>? lines, string? level = null)
    {
        var labels = lines?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList() ?? [];

        if (labels.Count == 0)
            return Empty(level);

        var items = labels.Select(s => (object)s).ToList();
        return NormalizeFromObjects(items, level);
    }

    public static string SerializeDocument(RubricDocumentV1 doc)
        => JsonSerializer.Serialize(doc, JsonOptions);

    /// <summary>Serialize criteria array for legacy EvaluationCriteriaJson field (full RubricV1 document).</summary>
    public static string SerializeForStorage(RubricDocumentV1 doc)
        => SerializeDocument(doc);

    /// <summary>Text hiển thị HR — một dòng mỗi tiêu chí (cache ScoringRubric).</summary>
    public static string ToDisplayText(RubricDocumentV1? doc)
    {
        if (doc?.Criteria is not { Count: > 0 })
            return string.Empty;

        return string.Join(
            "\n",
            doc.Criteria.Select(c =>
                $"[{c.Weight}%] {c.Label}"));
    }

    /// <summary>Publish gate: ≥1 criterion, weight=100, mỗi criterion ≥2 anchors.</summary>
    public static bool IsPublishReady(RubricDocumentV1? doc)
    {
        if (doc?.Criteria is not { Count: > 0 })
            return false;

        var sum = doc.Criteria.Sum(c => c.Weight);
        if (sum != 100)
            return false;

        return doc.Criteria.All(c =>
            !string.IsNullOrWhiteSpace(c.Label)
            && c.Weight > 0
            && c.Anchors.Count >= 2
            && c.Anchors.Values.Any(v => !string.IsNullOrWhiteSpace(v)));
    }

    /// <summary>Giai đoạn 1: flatten cho evaluate-answer legacy (anchor inline).</summary>
    public static List<string> FlattenForEvaluate(RubricDocumentV1? doc)
    {
        if (doc?.Criteria is not { Count: > 0 })
            return [];

        return doc.Criteria.Select(c =>
        {
            var anchorParts = c.Anchors
                .OrderBy(kv => int.TryParse(kv.Key, out var n) ? n : 0)
                .Select(kv => $"{kv.Key}: {kv.Value}");
            var anchorText = string.Join("; ", anchorParts);
            return string.IsNullOrWhiteSpace(anchorText)
                ? $"[{c.Weight}%] {c.Label}"
                : $"[{c.Weight}%] {c.Label} — Mốc: {anchorText}";
        }).ToList();
    }

    /// <summary>Merge RAG criteria objects + optional level into RubricV1.</summary>
    public static RubricDocumentV1 NormalizeFromRagCriteria(
        IEnumerable<object>? raw,
        string? level = null)
        => NormalizeFromObjects(raw, level);

    private static RubricDocumentV1 Empty(string? level)
        => new() { Version = RubricVersion, Scale = DefaultScale, Level = level, Criteria = [] };

    private static RubricDocumentV1 ParseDocumentObject(JsonElement root, string? level)
    {
        var doc = new RubricDocumentV1
        {
            Version = root.TryGetProperty("version", out var v) && v.TryGetInt32(out var ver) ? ver : RubricVersion,
            Scale = ReadString(root, "scale") ?? DefaultScale,
            Level = ReadString(root, "level") ?? level,
            Criteria = []
        };

        if (!root.TryGetProperty("criteria", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return doc;

        foreach (var el in arr.EnumerateArray())
        {
            var criterion = ParseCriterionElement(el);
            if (criterion is not null)
                doc.Criteria.Add(criterion);
        }

        RebalanceWeightsIfNeeded(doc);
        return doc;
    }

    private static List<JsonElement> ParseArrayItems(JsonElement array)
    {
        var items = new List<JsonElement>();
        foreach (var el in array.EnumerateArray())
            items.Add(el.Clone());
        return items;
    }

    private static RubricDocumentV1 FromLegacyItems(IReadOnlyList<JsonElement> items, string? level)
    {
        var criteria = new List<RubricCriterionV1>();
        var index = 0;

        foreach (var el in items)
        {
            RubricCriterionV1? c = el.ValueKind switch
            {
                JsonValueKind.String => CreateCriterionFromLabel(el.GetString() ?? "", index++),
                JsonValueKind.Object => ParseCriterionElement(el) ?? CreateCriterionFromLabel(ReadString(el, "label", "text", "criterion", "name") ?? "", index++),
                _ => null
            };
            if (c is not null && !string.IsNullOrWhiteSpace(c.Label))
                criteria.Add(c);
        }

        if (criteria.Count == 0)
            return Empty(level);

        DistributeWeightsEvenly(criteria);

        return new RubricDocumentV1
        {
            Version = RubricVersion,
            Scale = DefaultScale,
            Level = level,
            Criteria = criteria
        };
    }

    private static RubricCriterionV1? ParseCriterionElement(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
            return null;

        var label = ReadString(el, "label", "text", "criterion", "name");
        if (string.IsNullOrWhiteSpace(label))
            return null;

        var id = ReadString(el, "id") ?? Slugify(label);
        var weight = ReadInt(el, "weight") ?? 0;

        var anchors = new Dictionary<string, string>();
        if (el.TryGetProperty("anchors", out var anchorsEl) && anchorsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in anchorsEl.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var val = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(val))
                        anchors[prop.Name] = val.Trim();
                }
            }
        }

        if (anchors.Count == 0)
            anchors = DefaultAnchorsForLabel(label);

        return new RubricCriterionV1
        {
            Id = id,
            Label = label.Trim(),
            Weight = weight,
            Anchors = anchors
        };
    }

    private static RubricCriterionV1 CreateCriterionFromLabel(string label, int index)
    {
        var trimmed = label.Trim();
        return new RubricCriterionV1
        {
            Id = Slugify(trimmed, $"criterion-{index + 1}"),
            Label = trimmed,
            Weight = 0,
            Anchors = DefaultAnchorsForLabel(trimmed)
        };
    }

    private static Dictionary<string, string> DefaultAnchorsForLabel(string label)
    {
        return new Dictionary<string, string>
        {
            ["25"] = $"Chưa đạt — thiếu hoặc sai về {label}",
            ["50"] = $"Cơ bản — nêu được ý chính về {label}",
            ["75"] = $"Khá — giải thích rõ và có ví dụ về {label}",
            ["100"] = $"Xuất sắc — sâu, chính xác, có edge case liên quan {label}"
        };
    }

    private static void DistributeWeightsEvenly(List<RubricCriterionV1> criteria)
    {
        if (criteria.Count == 0)
            return;

        if (criteria.All(c => c.Weight > 0) && criteria.Sum(c => c.Weight) == 100)
            return;

        var baseWeight = 100 / criteria.Count;
        var remainder = 100 - baseWeight * criteria.Count;
        for (var i = 0; i < criteria.Count; i++)
            criteria[i].Weight = baseWeight + (i < remainder ? 1 : 0);
    }

    private static void RebalanceWeightsIfNeeded(RubricDocumentV1 doc)
    {
        if (doc.Criteria.Count == 0)
            return;

        if (doc.Criteria.All(c => c.Weight > 0) && doc.Criteria.Sum(c => c.Weight) == 100)
            return;

        DistributeWeightsEvenly(doc.Criteria);
    }

    private static string? ReadString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var p))
                continue;
            if (p.ValueKind == JsonValueKind.String)
            {
                var v = p.GetString();
                if (!string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            }
        }
        return null;
    }

    private static int? ReadInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n))
            return n;
        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var parsed))
            return parsed;
        return null;
    }

    private static string Slugify(string text, string fallback = "criterion")
    {
        var slug = new string(text
            .ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || ch == '-')
            .ToArray());
        if (string.IsNullOrWhiteSpace(slug))
            slug = fallback;
        return slug.Length > 40 ? slug[..40] : slug;
    }
}

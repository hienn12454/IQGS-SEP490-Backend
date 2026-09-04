using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// SCRUM-434: Sau map RAG plan — đảm bảo PlanFocusAreas + coverage đủ mọi skill JD.
/// Giữ source/weight đã match; skill thiếu được append rồi equal-split % = 100.
/// Không áp dụng cho EXCLUSIVE refine (caller chỉ gọi trên generate initial).
/// </summary>
public static class StudioPlanFocusJdCompleter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static StudioRagPlanMapper.MappedPlan EnsureAllJdSkills(
        StudioRagPlanMapper.MappedPlan mapped,
        IReadOnlyList<string>? jdSkills,
        int totalQuestions)
    {
        var catalog = NormalizeCatalog(jdSkills);
        if (catalog.Count == 0)
            return mapped;

        var totalQ = totalQuestions > 0 ? totalQuestions : mapped.TotalQuestions;
        if (totalQ <= 0) totalQ = 1;

        var completed = MergeFocusAreas(mapped.FocusAreas, catalog);
        var sourceJson = SyncCoverageInSourcePlanJson(mapped.SourcePlanJson, completed, totalQ);

        return mapped with
        {
            FocusAreas = completed,
            SourcePlanJson = sourceJson,
            TotalQuestions = totalQ
        };
    }

    /// <summary>Exact → head trước —/- → contains (skill dài nhất). Port FE matchJdSkill.</summary>
    public static string? MatchJdSkill(string name, IReadOnlyList<string> jdSkills)
    {
        var raw = name?.Trim() ?? "";
        if (raw.Length == 0 || jdSkills.Count == 0) return null;

        var lower = NormKey(raw);
        foreach (var s in jdSkills)
        {
            if (NormKey(s) == lower) return s;
        }

        var head = SplitHead(raw);
        if (!string.IsNullOrWhiteSpace(head))
        {
            var headLower = NormKey(head);
            foreach (var s in jdSkills)
            {
                if (NormKey(s) == headLower) return s;
            }
        }

        foreach (var skill in jdSkills.OrderByDescending(s => s.Length))
        {
            var sk = NormKey(skill);
            if (sk.Length >= 2 && lower.Contains(sk, StringComparison.Ordinal))
                return skill;
        }

        return null;
    }

    internal static IReadOnlyList<StudioRagPlanMapper.PlanFocusAreaDraft> MergeFocusAreas(
        IReadOnlyList<StudioRagPlanMapper.PlanFocusAreaDraft> ragFocus,
        IReadOnlyList<string> catalog)
    {
        // key = NormKey(catalog name) → draft (tên catalog, sources giữ từ RAG)
        var merged = new Dictionary<string, (string Name, IReadOnlyList<string> Sources)>(
            StringComparer.Ordinal);

        foreach (var f in ragFocus)
        {
            var matched = MatchJdSkill(f.Name, catalog);
            if (matched is null) continue;
            var key = NormKey(matched);
            if (merged.ContainsKey(key))
            {
                // Gộp sources nếu trùng skill
                var existing = merged[key];
                var sources = existing.Sources
                    .Concat(f.SourceFiles ?? Array.Empty<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                merged[key] = (existing.Name, sources);
            }
            else
            {
                merged[key] = (matched, f.SourceFiles ?? Array.Empty<string>());
            }
        }

        // Append skill JD còn thiếu — source mặc định job-description
        foreach (var skill in catalog)
        {
            var key = NormKey(skill);
            if (merged.ContainsKey(key)) continue;
            merged[key] = (skill, new[] { "job-description" });
        }

        // Giữ thứ tự catalog (JD) để UI ổn định
        var ordered = new List<(string Name, IReadOnlyList<string> Sources)>();
        foreach (var skill in catalog)
        {
            var key = NormKey(skill);
            if (merged.TryGetValue(key, out var row))
                ordered.Add(row);
        }

        var weights = EqualSplitPercents(ordered.Count);
        var result = new List<StudioRagPlanMapper.PlanFocusAreaDraft>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var (name, sources) = ordered[i];
            var src = sources.Count > 0 ? sources : new[] { "job-description" };
            result.Add(new StudioRagPlanMapper.PlanFocusAreaDraft(
                name,
                weights[i],
                i + 1,
                src));
        }

        return result;
    }

    /// <summary>Đồng bộ coverage/skills trong SourcePlanJson với focus đã đủ JD.</summary>
    internal static string SyncCoverageInSourcePlanJson(
        string sourcePlanJson,
        IReadOnlyList<StudioRagPlanMapper.PlanFocusAreaDraft> focusAreas,
        int totalQuestions)
    {
        if (string.IsNullOrWhiteSpace(sourcePlanJson) || focusAreas.Count == 0)
            return sourcePlanJson;

        try
        {
            var node = JsonNode.Parse(sourcePlanJson);
            if (node is not JsonObject root)
                return sourcePlanJson;

            // Unwrap { plan: {...} } nếu có
            var planObj = root;
            if (root["plan"] is JsonObject nested)
                planObj = nested;

            // Index coverage cũ theo skill key để giữ source_files RAG
            var oldSources = new Dictionary<string, JsonArray>(StringComparer.OrdinalIgnoreCase);
            if (planObj["coverage"] is JsonArray oldCoverage)
            {
                foreach (var item in oldCoverage.OfType<JsonObject>())
                {
                    var skill = item["skill"]?.GetValue<string>()
                        ?? item["Skill"]?.GetValue<string>()
                        ?? "";
                    if (string.IsNullOrWhiteSpace(skill)) continue;
                    var files = item["source_files"] as JsonArray
                        ?? item["sourceFiles"] as JsonArray;
                    if (files is not null)
                        oldSources[NormKey(skill)] = (files.DeepClone() as JsonArray) ?? new JsonArray();
                }
            }

            var counts = AllocateQuestionCounts(
                focusAreas.Select(f => (int)Math.Round(StudioFocusAreaWeightHelper.NormalizeToPercent(f.Weight))).ToList(),
                totalQuestions);

            var coverage = new JsonArray();
            var skillsArr = new JsonArray();
            for (var i = 0; i < focusAreas.Count; i++)
            {
                var f = focusAreas[i];
                var q = i < counts.Count ? counts[i] : 0;
                JsonArray sourceFiles;
                if (oldSources.TryGetValue(NormKey(f.Name), out var fromOld) && fromOld.Count > 0)
                {
                    sourceFiles = fromOld.DeepClone() as JsonArray
                        ?? new JsonArray { JsonValue.Create("job-description") };
                }
                else if (f.SourceFiles is { Count: > 0 })
                {
                    sourceFiles = new JsonArray();
                    foreach (var s in f.SourceFiles.Take(5))
                        sourceFiles.Add(JsonValue.Create(s));
                }
                else
                {
                    sourceFiles = new JsonArray { JsonValue.Create("job-description") };
                }

                coverage.Add(new JsonObject
                {
                    ["skill"] = f.Name,
                    ["questionCount"] = q,
                    ["question_count"] = q,
                    ["focusAreas"] = new JsonArray { JsonValue.Create(f.Name) },
                    ["focus_areas"] = new JsonArray { JsonValue.Create(f.Name) },
                    ["sourceFiles"] = sourceFiles.DeepClone() as JsonArray ?? sourceFiles,
                    ["source_files"] = sourceFiles.DeepClone() as JsonArray ?? sourceFiles
                });
                skillsArr.Add(JsonValue.Create(f.Name));
            }

            planObj["coverage"] = coverage;
            planObj["skills"] = skillsArr;
            planObj["totalQuestions"] = totalQuestions;
            planObj["total_questions"] = totalQuestions;

            return root.ToJsonString(JsonOptions);
        }
        catch
        {
            return sourcePlanJson;
        }
    }

    private static List<string> NormalizeCatalog(IReadOnlyList<string>? jdSkills)
    {
        if (jdSkills is null || jdSkills.Count == 0) return [];
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in jdSkills)
        {
            var t = s?.Trim();
            if (string.IsNullOrWhiteSpace(t)) continue;
            if (!seen.Add(t)) continue;
            list.Add(t);
        }
        return list;
    }

    private static string NormKey(string s) => s.Trim().ToLowerInvariant();

    private static string SplitHead(string raw)
    {
        foreach (var sep in new[] { "—", "–", "-" })
        {
            var i = raw.IndexOf(sep, StringComparison.Ordinal);
            if (i > 0) return raw[..i].Trim();
        }
        return raw;
    }

    /// <summary>Giống FE equalSplitFocusWeightsTo100: base + rem từ đầu.</summary>
    private static List<decimal> EqualSplitPercents(int n)
    {
        if (n <= 0) return [];
        if (n == 1) return [100m];
        var bas = 100 / n;
        var rem = 100 % n;
        var list = new List<decimal>(n);
        for (var i = 0; i < n; i++)
            list.Add(bas + (i < rem ? 1 : 0));
        return list;
    }

    /// <summary>Largest-remainder theo weight; cho phép 0 khi total &lt; n.</summary>
    private static List<int> AllocateQuestionCounts(IReadOnlyList<int> weights, int total)
    {
        var n = weights.Count;
        var result = new int[n];
        if (n == 0 || total <= 0) return result.ToList();

        var sumW = weights.Sum();
        if (sumW <= 0)
        {
            var bas = total / n;
            var rem = total % n;
            for (var i = 0; i < n; i++)
                result[i] = bas + (i < rem ? 1 : 0);
            return result.ToList();
        }

        var exact = new double[n];
        var floors = new int[n];
        var frac = new (int Index, double Frac)[n];
        var assigned = 0;
        for (var i = 0; i < n; i++)
        {
            exact[i] = total * (weights[i] / (double)sumW);
            floors[i] = (int)Math.Floor(exact[i]);
            assigned += floors[i];
            frac[i] = (i, exact[i] - floors[i]);
        }

        var left = total - assigned;
        foreach (var (index, _) in frac.OrderByDescending(x => x.Frac).ThenBy(x => x.Index))
        {
            if (left <= 0) break;
            floors[index]++;
            left--;
        }

        return floors.ToList();
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using DomainLayer.Studio.Enums;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>SCRUM-389: Lọc coverage/focus về ALLOWED_TOPICS sau RAG exclusive refine.</summary>
public static class StudioExclusiveCoverageFilter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static StudioRagPlanMapper.MappedPlan Filter(
        StudioRagPlanMapper.MappedPlan mapped,
        IReadOnlyList<string> allowedTopics,
        int totalQuestions,
        int minutes,
        QuestionDifficulty difficulty)
    {
        if (allowedTopics.Count == 0)
            return mapped;

        var keptFocus = mapped.FocusAreas
            .Where(f => MatchesAny(f.Name, allowedTopics))
            .Select((f, i) => f with { OrderIndex = i })
            .ToList();

        if (keptFocus.Count == 0)
        {
            // Gộp thành 1 focus từ topic đầu
            var topic = allowedTopics[0];
            keptFocus =
            [
                new StudioRagPlanMapper.PlanFocusAreaDraft(topic, 1.0m, 0, Array.Empty<string>())
            ];
        }
        else
        {
            // Normalize weights
            var sum = keptFocus.Sum(f => f.Weight);
            if (sum <= 0) sum = keptFocus.Count;
            keptFocus = keptFocus
                .Select((f, i) => f with { Weight = Math.Round(f.Weight / sum, 4), OrderIndex = i })
                .ToList();
        }

        var patchedJson = PatchCoverageJson(mapped.SourcePlanJson, allowedTopics, totalQuestions);
        return mapped with
        {
            FocusAreas = keptFocus,
            SourcePlanJson = patchedJson ?? mapped.SourcePlanJson,
            TotalQuestions = totalQuestions,
            InterviewLengthMinutes = minutes,
            Difficulty = difficulty
        };
    }

    public static bool HasDisallowedFocus(
        StudioRagPlanMapper.MappedPlan mapped,
        IReadOnlyList<string> allowedTopics)
    {
        if (allowedTopics.Count == 0 || mapped.FocusAreas.Count == 0)
            return false;
        return mapped.FocusAreas.Any(f => !MatchesAny(f.Name, allowedTopics));
    }

    private static bool MatchesAny(string name, IReadOnlyList<string> allowed)
    {
        var n = name.ToLowerInvariant();
        foreach (var a in allowed)
        {
            var t = a.Trim().ToLowerInvariant();
            if (t.Length == 0) continue;
            if (n.Contains(t, StringComparison.Ordinal) || t.Contains(n, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string? PatchCoverageJson(string? sourcePlanJson, IReadOnlyList<string> allowed, int totalQuestions)
    {
        if (string.IsNullOrWhiteSpace(sourcePlanJson)) return null;
        try
        {
            var root = JsonNode.Parse(sourcePlanJson)?.AsObject();
            if (root is null) return null;
            var plan = root["plan"]?.AsObject() ?? root;
            if (plan["coverage"] is JsonArray cov)
            {
                var kept = new JsonArray();
                foreach (var item in cov)
                {
                    if (item is not JsonObject o) continue;
                    var skill = o["skill"]?.GetValue<string>()
                                ?? o["name"]?.GetValue<string>()
                                ?? "";
                    if (MatchesAny(skill, allowed))
                        kept.Add(item.DeepClone());
                }
                if (kept.Count == 0)
                {
                    kept.Add(new JsonObject
                    {
                        ["skill"] = allowed[0],
                        ["question_count"] = totalQuestions,
                        ["questionCount"] = totalQuestions,
                        ["focus_areas"] = new JsonArray(allowed[0]),
                        ["source_files"] = new JsonArray()
                    });
                }
                plan["coverage"] = kept;
            }
            if (plan["skills"] is JsonArray skills)
            {
                var keptSkills = new JsonArray();
                foreach (var s in skills)
                {
                    var v = s?.GetValue<string>() ?? "";
                    if (MatchesAny(v, allowed))
                        keptSkills.Add(v);
                }
                if (keptSkills.Count == 0)
                    keptSkills.Add(allowed[0]);
                plan["skills"] = keptSkills;
            }
            return root.ToJsonString(JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}

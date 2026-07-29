using System.Text.Json;
using System.Text.Json.Nodes;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// SCRUM-376: Áp dụng quick controls vào plan bằng patch local (không gọi RAG),
/// giữ focus/coverage/outline soft requirements từ chat refine trước đó.
/// </summary>
public static class StudioPlanSettingsPatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public sealed record SectionInput(
        string Name,
        string? Description,
        int OrderIndex,
        int NumberOfQuestions,
        QuestionDifficulty Difficulty,
        int EstimatedMinutes);

    public sealed record FocusInput(string Name, decimal Weight, int OrderIndex);

    /// <summary>
    /// Patch SourcePlanJson + scale sections/focus theo TARGET settings.
    /// </summary>
    public static StudioRagPlanMapper.MappedPlan Apply(
        InterviewPlan source,
        IReadOnlyList<SectionInput> sourceSections,
        IReadOnlyList<FocusInput> sourceFocus,
        int targetTotal,
        QuestionDifficulty targetDifficulty,
        int targetMinutes,
        IReadOnlyList<string> questionTypes)
    {
        if (targetTotal < 1)
            throw new ArgumentOutOfRangeException(nameof(targetTotal), "Số câu phải ≥ 1.");
        if (questionTypes.Count == 0)
            throw new ArgumentException("Cần ít nhất 1 loại câu hỏi.", nameof(questionTypes));

        var patchedJson = PatchSourcePlanJson(
            source.SourcePlanJson,
            targetTotal,
            targetDifficulty,
            targetMinutes,
            questionTypes,
            source);

        // Map lại sections từ JSON đã patch (type distribution / outline)
        var mapped = StudioRagPlanMapper.MapFromRagPlanObject(patchedJson, targetTotal, targetMinutes);

        // Giữ focus areas từ plan cũ (chat refine) — không để mapper ghi đè tên/weight
        var focus = sourceFocus.Count > 0
            ? sourceFocus
                .OrderBy(f => f.OrderIndex)
                .Select(f => new StudioRagPlanMapper.PlanFocusAreaDraft(f.Name, f.Weight, f.OrderIndex, Array.Empty<string>()))
                .ToList()
            : mapped.FocusAreas.ToList();

        // Ưu tiên scale sections DB nếu cùng số nhóm / có thể map theo tên; không thì dùng mapped
        var sections = ScaleOrRebuildSections(
            sourceSections,
            mapped.Sections,
            questionTypes,
            targetTotal,
            targetMinutes,
            targetDifficulty);

        return new StudioRagPlanMapper.MappedPlan(
            Title: mapped.Title,
            Summary: mapped.Summary,
            TotalQuestions: targetTotal,
            InterviewLengthMinutes: targetMinutes,
            SeniorityLevel: string.IsNullOrWhiteSpace(source.SeniorityLevel) ? mapped.SeniorityLevel : source.SeniorityLevel,
            Difficulty: targetDifficulty,
            SourcePlanJson: patchedJson,
            Sections: sections,
            FocusAreas: focus,
            EasyCount: mapped.EasyCount,
            MediumCount: mapped.MediumCount,
            HardCount: mapped.HardCount);
    }

    private static string PatchSourcePlanJson(
        string? sourcePlanJson,
        int targetTotal,
        QuestionDifficulty targetDifficulty,
        int targetMinutes,
        IReadOnlyList<string> questionTypes,
        InterviewPlan source)
    {
        JsonObject root;
        if (string.IsNullOrWhiteSpace(sourcePlanJson))
        {
            root = new JsonObject
            {
                ["roleTitle"] = Truncate(source.Title, 80),
                ["summary"] = source.Title,
                ["experienceLevel"] = source.SeniorityLevel?.ToLowerInvariant() ?? "mid"
            };
        }
        else
        {
            var node = JsonNode.Parse(sourcePlanJson)
                ?? throw new InvalidOperationException("SourcePlanJson không parse được.");
            // Unwrap { plan: {...} }
            if (node is JsonObject wrap && wrap["plan"] is JsonObject nested)
                root = nested;
            else if (node is JsonObject obj)
                root = obj;
            else
                throw new InvalidOperationException("SourcePlanJson không phải object.");
        }

        root["totalQuestions"] = targetTotal;
        root["total_questions"] = targetTotal;
        root["difficulty"] = targetDifficulty.ToString().ToLowerInvariant();
        root["level"] = targetDifficulty.ToString().ToLowerInvariant();
        root["interviewLengthMinutes"] = targetMinutes;

        var typeCounts = AllocateEvenCounts(targetTotal, questionTypes);
        root["questionTypeDistribution"] = BuildTypeDistribution(questionTypes, typeCounts, root);
        root["question_type_distribution"] = root["questionTypeDistribution"]!.DeepClone();

        var (easy, medium, hard) = ScaleDifficultyMix(root, targetTotal, targetDifficulty);
        root["difficultyDistribution"] = new JsonArray
        {
            new JsonObject { ["difficulty"] = "easy", ["count"] = easy },
            new JsonObject { ["difficulty"] = "medium", ["count"] = medium },
            new JsonObject { ["difficulty"] = "hard", ["count"] = hard }
        };
        root["difficulty_distribution"] = root["difficultyDistribution"]!.DeepClone();

        PatchOutline(root, targetTotal, questionTypes, typeCounts, targetDifficulty, easy, medium, hard);
        PatchCoverageCounts(root, targetTotal);

        return root.ToJsonString(JsonOptions);
    }

    private static JsonArray BuildTypeDistribution(
        IReadOnlyList<string> questionTypes,
        IReadOnlyList<int> counts,
        JsonObject root)
    {
        // Giữ reason cũ nếu type trùng
        var oldReasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "questionTypeDistribution", "question_type_distribution" })
        {
            if (root[key] is not JsonArray arr) continue;
            foreach (var item in arr.OfType<JsonObject>())
            {
                var type = item["type"]?.GetValue<string>();
                var reason = item["reason"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(type) && !string.IsNullOrWhiteSpace(reason))
                    oldReasons[type!] = reason!;
            }
        }

        var result = new JsonArray();
        for (var i = 0; i < questionTypes.Count; i++)
        {
            var type = questionTypes[i];
            oldReasons.TryGetValue(type, out var reason);
            result.Add(new JsonObject
            {
                ["type"] = type,
                ["count"] = counts[i],
                ["reason"] = reason ?? $"Phân bổ {type} theo quick controls Studio."
            });
        }
        return result;
    }

    private static (int Easy, int Medium, int Hard) ScaleDifficultyMix(
        JsonObject root,
        int targetTotal,
        QuestionDifficulty overall)
    {
        var easy = 0;
        var medium = 0;
        var hard = 0;
        var found = false;

        JsonArray? arr = root["difficultyDistribution"] as JsonArray
            ?? root["difficulty_distribution"] as JsonArray;
        if (arr is { Count: > 0 })
        {
            foreach (var item in arr.OfType<JsonObject>())
            {
                var d = StudioRagPlanMapper.MapDifficulty(
                    item["difficulty"]?.GetValue<string>() ?? item["level"]?.GetValue<string>());
                var c = item["count"]?.GetValue<int>() ?? 0;
                if (c <= 0) continue;
                found = true;
                switch (d)
                {
                    case QuestionDifficulty.Easy: easy += c; break;
                    case QuestionDifficulty.Hard: hard += c; break;
                    default: medium += c; break;
                }
            }
        }

        var sum = easy + medium + hard;
        if (!found || sum <= 0)
            return AllocateDefaultMix(targetTotal, overall);

        var scale = targetTotal / (double)sum;
        easy = Math.Max(0, (int)Math.Round(easy * scale));
        medium = Math.Max(0, (int)Math.Round(medium * scale));
        hard = Math.Max(0, (int)Math.Round(hard * scale));
        var drift = targetTotal - (easy + medium + hard);
        medium = Math.Max(0, medium + drift);
        return (easy, medium, hard);
    }

    private static (int Easy, int Medium, int Hard) AllocateDefaultMix(int total, QuestionDifficulty overall)
    {
        double[] w = overall switch
        {
            QuestionDifficulty.Easy => [0.50, 0.35, 0.15],
            QuestionDifficulty.Hard => [0.15, 0.35, 0.50],
            _ => [0.25, 0.40, 0.35]
        };
        var parts = AllocateByWeights(total, w);
        return (parts[0], parts[1], parts[2]);
    }

    private static void PatchOutline(
        JsonObject root,
        int targetTotal,
        IReadOnlyList<string> questionTypes,
        IReadOnlyList<int> typeCounts,
        QuestionDifficulty overall,
        int easy,
        int medium,
        int hard)
    {
        var oldItems = new List<JsonObject>();
        foreach (var key in new[] { "recommendedQuestionOutline", "recommended_question_outline" })
        {
            if (root[key] is not JsonArray arr) continue;
            foreach (var item in arr.OfType<JsonObject>())
                oldItems.Add(item);
            break;
        }

        var difficultyQueue = BuildDifficultyQueue(easy, medium, hard, targetTotal, overall);
        var outline = new JsonArray();
        var qIdx = 0;

        for (var t = 0; t < questionTypes.Count; t++)
        {
            var type = questionTypes[t];
            var count = typeCounts[t];
            var pool = oldItems
                .Where(i => TypesMatch(i["type"]?.GetValue<string>(), type))
                .ToList();
            for (var i = 0; i < count; i++)
            {
                var template = pool.Count > 0 ? pool[i % pool.Count] : null;
                var diff = qIdx < difficultyQueue.Count
                    ? difficultyQueue[qIdx].ToString().ToLowerInvariant()
                    : overall.ToString().ToLowerInvariant();
                // RAG QuestionGenerationPlan bắt buộc mỗi outline item có order (1-based)
                var order = qIdx + 1;
                qIdx++;
                outline.Add(new JsonObject
                {
                    ["order"] = order,
                    ["type"] = type,
                    ["difficulty"] = diff,
                    ["skill"] = template?["skill"]?.GetValue<string>()
                        ?? StudioRagPlanMapper.FriendlySectionName(type),
                    ["focusArea"] = template?["focusArea"]?.GetValue<string>()
                        ?? template?["focus_area"]?.GetValue<string>()
                        ?? StudioRagPlanMapper.FriendlySectionName(type),
                    ["goal"] = template?["goal"]?.GetValue<string>()
                        ?? $"Đánh giá năng lực {type} theo yêu cầu đã refine trước đó."
                });
            }
        }

        // Nếu thiếu (do typeCounts lệch) — pad từ oldItems
        while (outline.Count < targetTotal && oldItems.Count > 0)
        {
            var template = oldItems[outline.Count % oldItems.Count];
            outline.Add(template.DeepClone());
        }
        while (outline.Count > targetTotal)
            outline.RemoveAt(outline.Count - 1);

        // Đảm bảo order liên tục 1..N sau pad/trim (clone cũ có thể thiếu/trùng order)
        for (var i = 0; i < outline.Count; i++)
        {
            if (outline[i] is JsonObject item)
            {
                item["order"] = i + 1;
            }
        }

        root["recommendedQuestionOutline"] = outline;
        root["recommended_question_outline"] = outline.DeepClone();
    }

    private static void PatchCoverageCounts(JsonObject root, int targetTotal)
    {
        if (root["coverage"] is not JsonArray coverage || coverage.Count == 0)
            return;

        var items = coverage.OfType<JsonObject>().ToList();
        if (items.Count == 0) return;

        var oldCounts = items
            .Select(i => i["questionCount"]?.GetValue<int>()
                ?? i["question_count"]?.GetValue<int>()
                ?? 1)
            .ToList();
        var sum = Math.Max(1, oldCounts.Sum());
        var left = targetTotal;
        for (var i = 0; i < items.Count; i++)
        {
            var c = i == items.Count - 1
                ? Math.Max(1, left)
                : Math.Max(1, (int)Math.Round(oldCounts[i] * (targetTotal / (double)sum)));
            if (i < items.Count - 1) left -= c;
            items[i]["questionCount"] = c;
            items[i]["question_count"] = c;
        }
    }

    private static IReadOnlyList<StudioRagPlanMapper.PlanSectionDraft> ScaleOrRebuildSections(
        IReadOnlyList<SectionInput> sourceSections,
        IReadOnlyList<StudioRagPlanMapper.PlanSectionDraft> mappedSections,
        IReadOnlyList<string> questionTypes,
        int targetTotal,
        int targetMinutes,
        QuestionDifficulty targetDifficulty)
    {
        if (sourceSections.Count == 0)
            return mappedSections;

        // Nếu số section ≈ số type và tên map được → scale counts, giữ tên/mô tả chat
        if (sourceSections.Count == questionTypes.Count || sourceSections.Count == mappedSections.Count)
        {
            var scaledCounts = AllocateProportional(
                sourceSections.Select(s => Math.Max(1, s.NumberOfQuestions)).ToList(),
                targetTotal);
            var result = new List<StudioRagPlanMapper.PlanSectionDraft>();
            for (var i = 0; i < sourceSections.Count; i++)
            {
                var s = sourceSections[i];
                var count = scaledCounts[i];
                result.Add(new StudioRagPlanMapper.PlanSectionDraft(
                    Name: s.Name,
                    Description: s.Description,
                    OrderIndex: s.OrderIndex > 0 ? s.OrderIndex : i + 1,
                    NumberOfQuestions: count,
                    Difficulty: s.Difficulty == default ? targetDifficulty : s.Difficulty,
                    EstimatedMinutes: Math.Max(5, (int)Math.Round(targetMinutes * (count / (double)targetTotal)))));
            }
            return result;
        }

        return mappedSections;
    }

    private static List<int> AllocateEvenCounts(int total, IReadOnlyList<string> types)
    {
        var n = types.Count;
        if (n == 0 || total <= 0) return [];
        // Phân bổ đều; nếu total < n vẫn đảm bảo mỗi type ≥ 1 bằng cách tăng total ảo rồi clamp — thực tế settings min=5
        var weights = Enumerable.Repeat(1.0 / n, n).ToArray();
        var parts = AllocateByWeights(Math.Max(total, n), weights);
        // Scale về đúng total nếu đã inflate
        if (parts.Sum() != total)
        {
            var scaled = AllocateProportional(parts.Select(p => Math.Max(1, p)).ToList(), total);
            return scaled;
        }
        for (var i = 0; i < parts.Length; i++)
            if (parts[i] < 1) parts[i] = 1;
        var drift = total - parts.Sum();
        if (drift != 0)
            parts[^1] = Math.Max(1, parts[^1] + drift);
        return parts.ToList();
    }

    private static List<int> AllocateProportional(IReadOnlyList<int> weights, int total)
    {
        var n = weights.Count;
        var result = new int[n];
        if (n == 0 || total <= 0) return result.ToList();
        var sumW = Math.Max(1, weights.Sum());
        var left = total;
        for (var i = 0; i < n; i++)
        {
            if (i == n - 1)
            {
                result[i] = Math.Max(1, left);
                break;
            }
            result[i] = Math.Max(1, (int)Math.Round(weights[i] * (total / (double)sumW)));
            left -= result[i];
        }
        var drift = total - result.Sum();
        if (drift != 0)
            result[^1] = Math.Max(1, result[^1] + drift);
        return result.ToList();
    }

    private static int[] AllocateByWeights(int total, double[] weights)
    {
        var result = new int[weights.Length];
        if (total <= 0) return result;
        var left = total;
        for (var i = 0; i < weights.Length; i++)
        {
            if (i == weights.Length - 1)
            {
                result[i] = Math.Max(0, left);
                break;
            }
            result[i] = Math.Max(0, (int)Math.Round(total * weights[i]));
            left -= result[i];
        }
        return result;
    }

    private static List<QuestionDifficulty> BuildDifficultyQueue(
        int easy, int medium, int hard, int total, QuestionDifficulty overall)
    {
        if (easy + medium + hard != total || total <= 0)
        {
            var mix = AllocateDefaultMix(total, overall);
            easy = mix.Easy;
            medium = mix.Medium;
            hard = mix.Hard;
        }
        var list = new List<QuestionDifficulty>(total);
        list.AddRange(Enumerable.Repeat(QuestionDifficulty.Easy, easy));
        list.AddRange(Enumerable.Repeat(QuestionDifficulty.Medium, medium));
        list.AddRange(Enumerable.Repeat(QuestionDifficulty.Hard, hard));
        return list.Select((d, i) => (d, i))
            .OrderBy(x => x.i % 3)
            .ThenBy(x => x.i)
            .Select(x => x.d)
            .ToList();
    }

    private static bool TypesMatch(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        static string Norm(string v) => v.Trim().ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
        return Norm(a) == Norm(b);
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}

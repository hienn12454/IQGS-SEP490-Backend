using System.Text.Json;
using ApplicationLayer.Studio.Contracts;
using DomainLayer.Studio.Enums;

namespace ApplicationLayer.Studio.Helpers;
/// <summary>
/// SCRUM-367: Map plan JSON từ RAG (giống JobPlanResponseDto / QuestionGenerationPlan)
/// → entity Studio InterviewPlan sections/focus areas + difficulty mix cho UI.
/// </summary>
public static class StudioRagPlanMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public sealed record MappedPlan(
        string Title,
        string? Summary,
        int TotalQuestions,
        int InterviewLengthMinutes,
        string SeniorityLevel,
        QuestionDifficulty Difficulty,
        string SourcePlanJson,
        IReadOnlyList<PlanSectionDraft> Sections,
        IReadOnlyList<PlanFocusAreaDraft> FocusAreas,
        int EasyCount,
        int MediumCount,
        int HardCount);

    public sealed record PlanSectionDraft(
        string Name,
        string? Description,
        int OrderIndex,
        int NumberOfQuestions,
        QuestionDifficulty Difficulty,
        int EstimatedMinutes);

    public sealed record PlanFocusAreaDraft(
        string Name,
        decimal Weight,
        int OrderIndex,
        IReadOnlyList<string> SourceFiles);

    public static MappedPlan MapFromRagPlanObject(object? planObj, int fallbackQuestionCount, int? preferredMinutes)
    {
        if (planObj is null)
            throw new InvalidOperationException("RAG không trả plan.");
        var json = planObj switch
        {
            JsonElement el => el.GetRawText(),
            string s => s,
            _ => JsonSerializer.Serialize(planObj, JsonOptions)
        };
        using var doc = JsonDocument.Parse(json);
        var root = UnwrapPlanRoot(doc.RootElement);
        var roleTitle = GetString(root, "roleTitle", "role_title") ?? "Interview Plan";
        var summary = GetString(root, "summary");
        var notes = GetString(root, "notes");
        if (string.IsNullOrWhiteSpace(summary) && !string.IsNullOrWhiteSpace(notes))
            summary = notes;
        var total = GetInt(root, "totalQuestions", "total_questions") ?? fallbackQuestionCount;
        if (total <= 0) total = fallbackQuestionCount;
        total = Math.Clamp(total, 5, 50);
        var experience = GetString(root, "experienceLevel", "experience_level") ?? "mid";
        var difficultyRaw = GetString(root, "difficulty") ?? GetString(root, "level") ?? "medium";
        var difficulty = MapDifficulty(difficultyRaw);
        var seniority = MapSeniority(experience);
        var minutes = preferredMinutes is > 0
            ? preferredMinutes.Value
            : Math.Clamp(total * 4, 30, 120);
        var (easy, medium, hard) = ParseDifficultyMix(root, total, difficulty);
        var sections = BuildSections(root, total, minutes, difficulty, easy, medium, hard);
        var focusAreas = BuildFocusAreas(root, total);
        // Canonical JSON (camelCase) để gửi lại khi generate questions
        var canonical = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json), JsonOptions);
        var titleCore = string.IsNullOrWhiteSpace(summary)
            ? $"Interview Plan for {roleTitle}"
            : $"{roleTitle}: {Truncate(summary!, 80)}";
        return new MappedPlan(
            Title: titleCore,
            Summary: summary,
            TotalQuestions: total,
            InterviewLengthMinutes: minutes,
            SeniorityLevel: seniority,
            Difficulty: difficulty,
            SourcePlanJson: canonical,
            Sections: sections,
            FocusAreas: focusAreas,
            EasyCount: easy,
            MediumCount: medium,
            HardCount: hard);
    }

    /// <summary>Ưu tiên difficulty_distribution trong SourcePlanJson (số câu Easy/Medium/Hard).</summary>
    public static (int Easy, int Medium, int Hard)? TryGetDifficultyMix(string? sourcePlanJson, int fallbackTotal)
    {
        if (string.IsNullOrWhiteSpace(sourcePlanJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(sourcePlanJson);
            var root = UnwrapPlanRoot(doc.RootElement);
            var total = GetInt(root, "totalQuestions", "total_questions") ?? fallbackTotal;
            if (total <= 0) total = fallbackTotal;
            var difficulty = MapDifficulty(GetString(root, "difficulty") ?? GetString(root, "level") ?? "medium");
            return ParseDifficultyMix(root, total, difficulty);
        }
        catch
        {
            return null;
        }
    }

    private static JsonElement UnwrapPlanRoot(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("plan", out var nested)
            && nested.ValueKind == JsonValueKind.Object)
            return nested;
        return root;
    }

    private static IReadOnlyList<PlanSectionDraft> BuildSections(
        JsonElement root,
        int totalQuestions,
        int totalMinutes,
        QuestionDifficulty overallDifficulty,
        int easy,
        int medium,
        int hard)
    {
        // 1) Ưu tiên question_type_distribution → section tên đẹp cho UI Studio
        if (TryBuildSectionsFromTypeDistribution(root, totalQuestions, totalMinutes, overallDifficulty, easy, medium, hard, out var fromDist)
            && fromDist.Count > 0)
            return fromDist;
        // 2) Group outline theo type
        if (TryGetArray(root, out var outline, "recommendedQuestionOutline", "recommended_question_outline")
            && outline.GetArrayLength() > 0)
        {
            var groups = new Dictionary<string, List<JsonElement>>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in outline.EnumerateArray())
            {
                var type = GetString(item, "type") ?? GetString(item, "focusArea", "focus_area") ?? "technical";
                if (!groups.TryGetValue(type, out var list))
                {
                    list = [];
                    groups[type] = list;
                }
                list.Add(item);
            }
            var result = new List<PlanSectionDraft>();
            var order = 1;
            var assigned = 0;
            var outlineLen = outline.GetArrayLength();
            foreach (var (rawType, items) in OrderSections(groups))
            {
                var count = items.Count;
                assigned += count;
                var diff = MajorityDifficulty(items) ?? overallDifficulty;
                var mins = Math.Max(5, (int)Math.Round(totalMinutes * (count / (double)Math.Max(1, outlineLen))));
                var goal = items.Select(i => GetString(i, "goal")).FirstOrDefault(g => !string.IsNullOrWhiteSpace(g));
                var skillHint = items.Select(i => GetString(i, "skill")).FirstOrDefault(g => !string.IsNullOrWhiteSpace(g));
                var desc = goal ?? (skillHint is null ? FriendlySectionDescription(rawType) : $"Focus: {skillHint}");
                result.Add(new PlanSectionDraft(
                    Name: FriendlySectionName(rawType),
                    Description: desc,
                    OrderIndex: order++,
                    NumberOfQuestions: count,
                    Difficulty: diff,
                    EstimatedMinutes: mins));
            }
            if (assigned < totalQuestions && result.Count > 0)
            {
                var last = result[^1];
                result[^1] = last with { NumberOfQuestions = last.NumberOfQuestions + (totalQuestions - assigned) };
            }
            return result;
        }
        // 3) Default 4 section Studio (mockup)
        return BuildDefaultStudioSections(totalQuestions, totalMinutes, overallDifficulty, easy, medium, hard);
    }

    private static bool TryBuildSectionsFromTypeDistribution(
        JsonElement root,
        int totalQuestions,
        int totalMinutes,
        QuestionDifficulty overallDifficulty,
        int easy,
        int medium,
        int hard,
        out List<PlanSectionDraft> sections)
    {
        sections = [];
        if (!TryGetArray(root, out var dist, "questionTypeDistribution", "question_type_distribution")
            || dist.GetArrayLength() == 0)
            return false;
        var order = 1;
        var sumCounts = 0;
        var drafts = new List<(string Type, int Count, string? Reason)>();
        foreach (var item in dist.EnumerateArray())
        {
            var type = GetString(item, "type") ?? $"section_{order}";
            var count = GetInt(item, "count") ?? 0;
            if (count <= 0) continue;
            drafts.Add((type, count, GetString(item, "reason")));
            sumCounts += count;
        }
        if (drafts.Count == 0) return false;
        // Scale nếu lệch total
        if (sumCounts != totalQuestions && sumCounts > 0)
        {
            var scaled = new List<(string Type, int Count, string? Reason)>();
            var left = totalQuestions;
            for (var i = 0; i < drafts.Count; i++)
            {
                var c = i == drafts.Count - 1
                    ? Math.Max(1, left)
                    : Math.Max(1, (int)Math.Round(drafts[i].Count * (totalQuestions / (double)sumCounts)));
                if (i < drafts.Count - 1) left -= c;
                scaled.Add((drafts[i].Type, c, drafts[i].Reason));
            }
            drafts = scaled;
        }
        var difficultyQueue = BuildDifficultyQueue(easy, medium, hard, totalQuestions, overallDifficulty);
        var qIdx = 0;
        foreach (var (type, count, reason) in OrderTypeDrafts(drafts))
        {
            var sectionDiffs = new List<QuestionDifficulty>();
            for (var i = 0; i < count && qIdx < difficultyQueue.Count; i++)
                sectionDiffs.Add(difficultyQueue[qIdx++]);
            var sectionDiff = sectionDiffs.Count == 0
                ? overallDifficulty
                : sectionDiffs.GroupBy(d => d).OrderByDescending(g => g.Count()).First().Key;
            sections.Add(new PlanSectionDraft(
                Name: FriendlySectionName(type),
                Description: reason ?? FriendlySectionDescription(type),
                OrderIndex: order++,
                NumberOfQuestions: count,
                Difficulty: sectionDiff,
                EstimatedMinutes: Math.Max(5, (int)Math.Round(totalMinutes * (count / (double)Math.Max(1, totalQuestions))))));
        }
        return sections.Count > 0;
    }

    private static IReadOnlyList<PlanSectionDraft> BuildDefaultStudioSections(
        int totalQuestions,
        int totalMinutes,
        QuestionDifficulty overallDifficulty,
        int easy,
        int medium,
        int hard)
    {
        // Tỷ lệ gần mockup: Tech 30% / SD 25% / PS 25% / Behavioral 20%
        var weights = new[] { 0.30, 0.25, 0.25, 0.20 };
        var names = new[] { "technical", "system_design", "problem_solving", "behavioral" };
        var counts = AllocateByWeights(totalQuestions, weights);
        var queue = BuildDifficultyQueue(easy, medium, hard, totalQuestions, overallDifficulty);
        var result = new List<PlanSectionDraft>();
        var qIdx = 0;
        for (var i = 0; i < names.Length; i++)
        {
            var count = counts[i];
            if (count <= 0) continue;
            var diffs = new List<QuestionDifficulty>();
            for (var j = 0; j < count && qIdx < queue.Count; j++)
                diffs.Add(queue[qIdx++]);
            var sectionDiff = diffs.Count == 0
                ? overallDifficulty
                : diffs.GroupBy(d => d).OrderByDescending(g => g.Count()).First().Key;
            result.Add(new PlanSectionDraft(
                Name: FriendlySectionName(names[i]),
                Description: FriendlySectionDescription(names[i]),
                OrderIndex: result.Count + 1,
                NumberOfQuestions: count,
                Difficulty: sectionDiff,
                EstimatedMinutes: Math.Max(5, (int)Math.Round(totalMinutes * (count / (double)Math.Max(1, totalQuestions))))));
        }
        return result;
    }

    /// <summary>SCRUM-369: Gắn source_files từ SourcePlanJson (coverage + citations) vào focus areas DB.</summary>
    public static IReadOnlyList<PlanFocusAreaItemDto> EnrichFocusAreasWithRagSources(
        IReadOnlyList<PlanFocusAreaItemDto> focusAreas,
        string? sourcePlanJson)
    {
        if (focusAreas.Count == 0)
            return focusAreas;

        var fromJson = TryBuildFocusSourcesFromPlanJson(sourcePlanJson);
        if (fromJson.CoverageSources.Count == 0 && fromJson.AllCitationFiles.Count == 0)
        {
            return focusAreas
                .Select(f => f.SourceFiles.Count > 0
                    ? f
                    : f with { SourceFiles = Array.Empty<string>() })
                .ToList();
        }

        var result = new List<PlanFocusAreaItemDto>();
        for (var i = 0; i < focusAreas.Count; i++)
        {
            var f = focusAreas[i];
            if (f.SourceFiles.Count > 0)
            {
                result.Add(f);
                continue;
            }

            var matched = MatchSourcesForFocus(f.Name, fromJson.CoverageSources, fromJson.AllCitationFiles);
            if (matched.Count == 0 && fromJson.AllCitationFiles.Count > 0)
                matched = [fromJson.AllCitationFiles[i % fromJson.AllCitationFiles.Count]];

            result.Add(f with { SourceFiles = matched });
        }
        return result;
    }

    public static IReadOnlyList<string> ExtractCitationSourceFiles(string? sourcePlanJson)
    {
        var built = TryBuildFocusSourcesFromPlanJson(sourcePlanJson);
        return built.AllCitationFiles;
    }

    /// <summary>SCRUM-370: Lấy loại câu từ question_type_distribution trong SourcePlanJson.</summary>
    public static List<string> ExtractQuestionTypesFromSourcePlan(string? sourcePlanJson)
    {
        if (string.IsNullOrWhiteSpace(sourcePlanJson)) return [];
        try
        {
            using var doc = JsonDocument.Parse(sourcePlanJson);
            var root = UnwrapPlanRoot(doc.RootElement);
            if (!TryGetArray(root, out var dist, "questionTypeDistribution", "question_type_distribution")
                || dist.GetArrayLength() == 0)
                return [];

            var types = new List<string>();
            foreach (var item in dist.EnumerateArray())
            {
                var type = GetString(item, "type");
                if (string.IsNullOrWhiteSpace(type)) continue;
                var normalized = StudioQuestionTypesHelper.Normalize([type]);
                foreach (var t in normalized)
                    if (!types.Contains(t, StringComparer.Ordinal))
                        types.Add(t);
            }
            return types;
        }
        catch
        {
            return [];
        }
    }

    private sealed record FocusSourceIndex(
        IReadOnlyList<(string SkillKey, IReadOnlyList<string> Files)> CoverageSources,
        IReadOnlyList<string> AllCitationFiles);

    private static FocusSourceIndex TryBuildFocusSourcesFromPlanJson(string? sourcePlanJson)
    {
        if (string.IsNullOrWhiteSpace(sourcePlanJson))
            return new FocusSourceIndex([], []);

        try
        {
            using var doc = JsonDocument.Parse(sourcePlanJson);
            var root = UnwrapPlanRoot(doc.RootElement);
            var coverageSources = new List<(string SkillKey, IReadOnlyList<string> Files)>();
            var allFiles = new List<string>();

            if (TryGetArray(root, out var coverage, "coverage") && coverage.GetArrayLength() > 0)
            {
                foreach (var item in coverage.EnumerateArray())
                {
                    var skill = GetString(item, "skill") ?? "";
                    var nested = "";
                    if (TryGetArray(item, out var nestedArr, "focusAreas", "focus_areas") && nestedArr.GetArrayLength() > 0
                        && nestedArr[0].ValueKind == JsonValueKind.String)
                        nested = nestedArr[0].GetString() ?? "";

                    var key = string.IsNullOrWhiteSpace(nested) ? skill : $"{skill} — {nested}";
                    var files = ReadStringList(item, "sourceFiles", "source_files");
                    coverageSources.Add((key, files));
                    foreach (var f in files)
                        if (!allFiles.Contains(f, StringComparer.OrdinalIgnoreCase))
                            allFiles.Add(f);
                }
            }

            if (TryGetArray(root, out var citations, "citations") && citations.GetArrayLength() > 0)
            {
                foreach (var cit in citations.EnumerateArray())
                {
                    var file = GetString(cit, "sourceFile", "source_file");
                    if (string.IsNullOrWhiteSpace(file)) continue;
                    if (!allFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                        allFiles.Add(file!);
                }
            }

            return new FocusSourceIndex(coverageSources, allFiles);
        }
        catch
        {
            return new FocusSourceIndex([], []);
        }
    }

    private static IReadOnlyList<string> MatchSourcesForFocus(
        string focusName,
        IReadOnlyList<(string SkillKey, IReadOnlyList<string> Files)> coverageSources,
        IReadOnlyList<string> allCitationFiles)
    {
        var name = focusName.Trim();
        foreach (var (skillKey, files) in coverageSources)
        {
            if (files.Count == 0) continue;
            if (string.Equals(skillKey, name, StringComparison.OrdinalIgnoreCase)
                || name.Contains(skillKey, StringComparison.OrdinalIgnoreCase)
                || skillKey.Contains(name, StringComparison.OrdinalIgnoreCase))
                return files.Take(3).ToList();
        }

        // Match theo token skill (phần trước "—")
        var skillPart = name.Split('—', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? name;
        foreach (var (skillKey, files) in coverageSources)
        {
            if (files.Count == 0) continue;
            var keyPart = skillKey.Split('—', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? skillKey;
            if (string.Equals(keyPart, skillPart, StringComparison.OrdinalIgnoreCase)
                || skillPart.Contains(keyPart, StringComparison.OrdinalIgnoreCase)
                || keyPart.Contains(skillPart, StringComparison.OrdinalIgnoreCase))
                return files.Take(3).ToList();
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> ReadStringList(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
                continue;
            var list = new List<string>();
            foreach (var item in p.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                var s = item.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(s) && !list.Contains(s, StringComparer.OrdinalIgnoreCase))
                    list.Add(s!);
            }
            return list;
        }
        return Array.Empty<string>();
    }

    private static IReadOnlyList<PlanFocusAreaDraft> BuildFocusAreas(JsonElement root, int totalQuestions)
    {
        var citationFiles = new List<string>();
        if (TryGetArray(root, out var citations, "citations") && citations.GetArrayLength() > 0)
        {
            foreach (var cit in citations.EnumerateArray())
            {
                var file = GetString(cit, "sourceFile", "source_file");
                if (!string.IsNullOrWhiteSpace(file) && !citationFiles.Contains(file!, StringComparer.OrdinalIgnoreCase))
                    citationFiles.Add(file!);
            }
        }

        if (TryGetArray(root, out var coverage, "coverage") && coverage.GetArrayLength() > 0)
        {
            var result = new List<PlanFocusAreaDraft>();
            var order = 1;
            var sumCount = 0;
            var counts = new List<(string Name, int Count, IReadOnlyList<string> Sources)>();
            var idx = 0;
            foreach (var item in coverage.EnumerateArray())
            {
                var name = GetString(item, "skill") ?? $"Focus {order}";
                var count = GetInt(item, "questionCount", "question_count") ?? 1;
                if (TryGetArray(item, out var nested, "focusAreas", "focus_areas") && nested.GetArrayLength() > 0)
                {
                    var nestedName = nested[0].ValueKind == JsonValueKind.String
                        ? nested[0].GetString()
                        : null;
                    if (!string.IsNullOrWhiteSpace(nestedName))
                        name = $"{name} — {nestedName}";
                }
                var sources = ReadStringList(item, "sourceFiles", "source_files").ToList();
                if (sources.Count == 0 && citationFiles.Count > 0)
                    sources.Add(citationFiles[idx % citationFiles.Count]);
                counts.Add((name!, count, sources));
                sumCount += count;
                idx++;
            }
            var denom = sumCount > 0 ? sumCount : totalQuestions;
            foreach (var (name, count, sources) in counts.Take(8))
            {
                var weight = Math.Round((decimal)count / denom, 2);
                result.Add(new PlanFocusAreaDraft(name, weight <= 0 ? 0.1m : weight, order++, sources));
            }
            return result;
        }
        return
        [
            new PlanFocusAreaDraft("Technical", 0.35m, 1, citationFiles.Take(1).ToList()),
            new PlanFocusAreaDraft("System Design", 0.30m, 2, citationFiles.Skip(1).Take(1).ToList()),
            new PlanFocusAreaDraft("Problem Solving", 0.20m, 3, citationFiles.Skip(2).Take(1).ToList()),
            new PlanFocusAreaDraft("Behavioral", 0.15m, 4, [])
        ];
    }

    private static (int Easy, int Medium, int Hard) ParseDifficultyMix(
        JsonElement root,
        int totalQuestions,
        QuestionDifficulty overall)
    {
        var easy = 0;
        var medium = 0;
        var hard = 0;
        var found = false;
        if (TryGetArray(root, out var arr, "difficultyDistribution", "difficulty_distribution")
            && arr.GetArrayLength() > 0)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    var d = MapDifficulty(GetString(item, "difficulty", "level") ?? "medium");
                    var c = GetInt(item, "count") ?? 0;
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
        }
        else if ((root.TryGetProperty("difficulty_distribution", out var obj)
                  || root.TryGetProperty("difficultyDistribution", out obj))
                 && obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in obj.EnumerateObject())
            {
                var c = p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out var n) ? n
                    : p.Value.ValueKind == JsonValueKind.String && int.TryParse(p.Value.GetString(), out n) ? n
                    : 0;
                if (c <= 0) continue;
                found = true;
                switch (MapDifficulty(p.Name))
                {
                    case QuestionDifficulty.Easy: easy += c; break;
                    case QuestionDifficulty.Hard: hard += c; break;
                    default: medium += c; break;
                }
            }
        }
        // Fallback: đếm từ outline
        if (!found
            && TryGetArray(root, out var outline, "recommendedQuestionOutline", "recommended_question_outline")
            && outline.GetArrayLength() > 0)
        {
            found = true;
            foreach (var item in outline.EnumerateArray())
            {
                switch (MapDifficulty(GetString(item, "difficulty") ?? "medium"))
                {
                    case QuestionDifficulty.Easy: easy++; break;
                    case QuestionDifficulty.Hard: hard++; break;
                    default: medium++; break;
                }
            }
        }
        var sum = easy + medium + hard;
        if (!found || sum <= 0)
            return AllocateDefaultMix(totalQuestions, overall);
        if (sum != totalQuestions)
        {
            // Scale về totalQuestions
            var scale = totalQuestions / (double)sum;
            easy = (int)Math.Round(easy * scale);
            medium = (int)Math.Round(medium * scale);
            hard = (int)Math.Round(hard * scale);
            var drift = totalQuestions - (easy + medium + hard);
            if (drift != 0) medium = Math.Max(0, medium + drift);
        }
        return (easy, medium, hard);
    }

    private static (int Easy, int Medium, int Hard) AllocateDefaultMix(int total, QuestionDifficulty overall)
    {
        // Tỷ lệ theo overall: easy-lean / balanced / hard-lean
        double[] w = overall switch
        {
            QuestionDifficulty.Easy => [0.50, 0.35, 0.15],
            QuestionDifficulty.Hard => [0.15, 0.35, 0.50],
            _ => [0.25, 0.40, 0.35]
        };
        var parts = AllocateByWeights(total, w);
        return (parts[0], parts[1], parts[2]);
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
        // Interleave nhẹ để mỗi section không toàn 1 mức
        return list.Select((d, i) => (d, i))
            .OrderBy(x => x.i % 3)
            .ThenBy(x => x.i)
            .Select(x => x.d)
            .ToList();
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
        // Đảm bảo không âm / không vượt
        if (left < 0)
        {
            for (var i = 0; i < result.Length && left < 0; i++)
            {
                var cut = Math.Min(result[i], -left);
                result[i] -= cut;
                left += cut;
            }
        }
        return result;
    }

    private static IEnumerable<KeyValuePair<string, List<JsonElement>>> OrderSections(
        Dictionary<string, List<JsonElement>> groups)
    {
        var preferred = new[] { "technical", "system_design", "systemdesign", "problem_solving", "problemsolving", "behavioral", "situational" };
        foreach (var key in preferred)
        {
            var match = groups.Keys.FirstOrDefault(k => NormalizeTypeKey(k) == NormalizeTypeKey(key));
            if (match is null) continue;
            yield return new KeyValuePair<string, List<JsonElement>>(match, groups[match]);
            groups.Remove(match);
        }
        foreach (var kv in groups.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            yield return kv;
    }

    private static List<(string Type, int Count, string? Reason)> OrderTypeDrafts(
        List<(string Type, int Count, string? Reason)> drafts)
    {
        var preferred = new[] { "technical", "system_design", "problem_solving", "behavioral" };
        var ordered = new List<(string Type, int Count, string? Reason)>();
        var rest = drafts.ToList();
        foreach (var p in preferred)
        {
            var idx = rest.FindIndex(x => NormalizeTypeKey(x.Type) == NormalizeTypeKey(p));
            if (idx < 0) continue;
            ordered.Add(rest[idx]);
            rest.RemoveAt(idx);
        }
        ordered.AddRange(rest);
        return ordered;
    }

    private static QuestionDifficulty? MajorityDifficulty(List<JsonElement> items)
    {
        if (items.Count == 0) return null;
        return items
            .Select(i => MapDifficulty(GetString(i, "difficulty") ?? "medium"))
            .GroupBy(d => d)
            .OrderByDescending(g => g.Count())
            .First()
            .Key;
    }

    public static string FriendlySectionName(string? rawType)
    {
        var key = NormalizeTypeKey(rawType);
        return key switch
        {
            "technical" or "technicalfoundations" => "Technical Foundations",
            "systemdesign" or "system" => "System Design",
            "problemsolving" or "coding" or "algorithm" => "Problem Solving",
            "behavioral" or "behavioural" => "Behavioral",
            "situational" or "culture" or "culturefit" => "Culture Fit",
            "followup" => "Follow-up",
            _ => Capitalize(rawType ?? "Section")
        };
    }

    private static string FriendlySectionDescription(string? rawType)
    {
        var key = NormalizeTypeKey(rawType);
        return key switch
        {
            "technical" or "technicalfoundations" => "Core fundamentals and role-specific technical depth",
            "systemdesign" or "system" => "Architecture, scalability and trade-offs",
            "problemsolving" or "coding" or "algorithm" => "Debugging, algorithms and optimization scenarios",
            "behavioral" or "behavioural" => "Communication, ownership and collaboration",
            "situational" or "culture" or "culturefit" => "Culture fit and workplace scenarios",
            _ => "Interview section generated from RAG plan"
        };
    }

    private static string NormalizeTypeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value.Trim().ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
    }

    public static QuestionDifficulty MapDifficulty(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return QuestionDifficulty.Medium;
        return raw.Trim().ToLowerInvariant() switch
        {
            "easy" or "intern" or "junior" => QuestionDifficulty.Easy,
            "hard" or "senior" or "lead" => QuestionDifficulty.Hard,
            _ => QuestionDifficulty.Medium
        };
    }

    public static string MapSeniority(string? experience)
    {
        if (string.IsNullOrWhiteSpace(experience)) return "Mid";
        return experience.Trim().ToLowerInvariant() switch
        {
            "intern" => "Intern",
            "junior" => "Junior",
            "senior" => "Senior",
            "lead" => "Lead",
            _ => "Mid"
        };
    }

    public static QuestionType MapQuestionType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return QuestionType.Technical;
        var s = NormalizeTypeKey(raw);
        return s switch
        {
            "behavioral" or "behavioural" => QuestionType.Behavioral,
            "systemdesign" or "system" => QuestionType.SystemDesign,
            "problemsolving" or "coding" or "algorithm" => QuestionType.ProblemSolving,
            "situational" or "culture" or "culturefit" => QuestionType.Situational,
            "followup" => QuestionType.FollowUp,
            _ => QuestionType.Technical
        };
    }

    private static bool TryGetArray(JsonElement root, out JsonElement array, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out array) && array.ValueKind == JsonValueKind.Array)
                return true;
        }
        array = default;
        return false;
    }

    private static string? GetString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        }
        return null;
    }

    private static int? GetInt(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var p)) continue;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n)) return n;
            if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out n)) return n;
        }
        return null;
    }

    private static string Capitalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Section";
        var t = value.Trim().Replace('_', ' ');
        return char.ToUpperInvariant(t[0]) + t[1..];
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}

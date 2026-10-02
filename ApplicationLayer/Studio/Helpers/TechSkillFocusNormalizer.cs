using ApplicationLayer.Studio.Contracts;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// Chuẩn hóa focus Studio về nhãn TechSkill và gộp trùng trước khi tạo plan.
/// HR đã có focus thì không append thêm mọi skill JD — chỉ gộp RAG với focus đó.
/// Focus trống thì seed từ skill JD map được sang enum.
/// </summary>
public static class TechSkillFocusNormalizer
{
    public static List<StudioFocusAreaItemDto> Canonicalize(IReadOnlyList<StudioFocusAreaItemDto>? areas)
    {
        var rows = new List<(string Label, decimal Weight, string? Description, string? SourceReason)>();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var area in areas ?? [])
        {
            var label = TechSkillCatalog.TryMap(area.Name);
            if (label is null) continue;
            var weight = StudioFocusAreaWeightHelper.NormalizeToPercent(area.Weight);
            if (index.TryGetValue(label, out var at))
            {
                var prev = rows[at];
                rows[at] = (
                    label,
                    prev.Weight + weight,
                    prev.Description ?? area.Description,
                    prev.SourceReason ?? area.SourceReason);
                continue;
            }

            index[label] = rows.Count;
            rows.Add((label, weight, area.Description, area.SourceReason));
        }

        return ToDtos(rows);
    }

    /// <summary>Skill JD không thuộc enum bị bỏ. Trùng alias (C# / csharp) chỉ một dòng.</summary>
    public static List<StudioFocusAreaItemDto> SeedFromJd(IReadOnlyList<string>? jdSkills)
    {
        var labels = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in jdSkills ?? [])
        {
            var label = TechSkillCatalog.TryMap(skill);
            if (label is null || !seen.Add(label)) continue;
            labels.Add(label);
        }

        if (labels.Count == 0) return [];
        var weights = EqualSplit(labels.Count);
        return labels
            .Select((label, i) => new StudioFocusAreaItemDto(label, weights[i], i, null, "job-description"))
            .ToList();
    }

    /// <summary>
    /// Gộp focus HR (đã canonical) với focus RAG. Cùng nhãn thì cộng weight, không tạo dòng thứ hai.
    /// Không thêm skill JD nằm ngoài hai danh sách này.
    /// </summary>
    public static StudioRagPlanMapper.MappedPlan MergeIntoPlan(
        StudioRagPlanMapper.MappedPlan mapped,
        IReadOnlyList<StudioFocusAreaItemDto> hrFocus)
    {
        var merged = MergeDrafts(mapped.FocusAreas, hrFocus);
        if (merged.Count == 0)
            return mapped;

        var sourceJson = StudioPlanFocusJdCompleter.SyncCoverageInSourcePlanJson(
            mapped.SourcePlanJson,
            merged,
            mapped.TotalQuestions > 0 ? mapped.TotalQuestions : 1);

        return mapped with
        {
            FocusAreas = merged,
            SourcePlanJson = sourceJson
        };
    }

    public static List<StudioRagPlanMapper.PlanFocusAreaDraft> MergeDrafts(
        IReadOnlyList<StudioRagPlanMapper.PlanFocusAreaDraft> ragFocus,
        IReadOnlyList<StudioFocusAreaItemDto> hrFocus)
    {
        var order = new List<string>();
        var weights = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var sources = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void Add(string? raw, decimal weight, IEnumerable<string>? src)
        {
            var label = TechSkillCatalog.TryMap(raw);
            if (label is null) return;
            if (!weights.ContainsKey(label))
            {
                order.Add(label);
                weights[label] = 0m;
                sources[label] = new List<string>();
            }

            weights[label] += StudioFocusAreaWeightHelper.NormalizeToPercent(weight);
            if (src is null) return;
            foreach (var file in src)
            {
                if (string.IsNullOrWhiteSpace(file)) continue;
                if (sources[label].Contains(file, StringComparer.OrdinalIgnoreCase)) continue;
                sources[label].Add(file);
            }
        }

        // HR trước để giữ thứ tự HR đã chọn.
        foreach (var area in hrFocus)
            Add(area.Name, area.Weight, null);
        foreach (var area in ragFocus)
            Add(area.Name, area.Weight, area.SourceFiles);

        if (order.Count == 0) return [];

        var scaled = ScaleTo100(order.Select(label => weights[label]).ToList());
        var result = new List<StudioRagPlanMapper.PlanFocusAreaDraft>(order.Count);
        for (var i = 0; i < order.Count; i++)
        {
            var label = order[i];
            var files = sources[label];
            if (files.Count == 0) files.Add("job-description");
            result.Add(new StudioRagPlanMapper.PlanFocusAreaDraft(label, scaled[i], i + 1, files));
        }

        return result;
    }

    private static List<StudioFocusAreaItemDto> ToDtos(
        List<(string Label, decimal Weight, string? Description, string? SourceReason)> rows)
    {
        if (rows.Count == 0) return [];
        var scaled = ScaleTo100(rows.Select(r => r.Weight).ToList());
        return rows.Select((row, i) => new StudioFocusAreaItemDto(
            row.Label,
            scaled[i],
            i,
            row.Description,
            row.SourceReason)).ToList();
    }

    /// <summary>Chia đều 100, phần dư dồn từ đầu — cùng quy ước FE equal-split.</summary>
    internal static List<decimal> EqualSplit(int count)
    {
        if (count <= 0) return [];
        if (count == 1) return [100m];
        var basePct = 100 / count;
        var rem = 100 % count;
        var list = new List<decimal>(count);
        for (var i = 0; i < count; i++)
            list.Add(basePct + (i < rem ? 1 : 0));
        return list;
    }

    /// <summary>Giữ tỉ lệ weight rồi làm tròn largest-remainder để tổng đúng 100.</summary>
    internal static List<decimal> ScaleTo100(IReadOnlyList<decimal> weights)
    {
        var count = weights.Count;
        if (count == 0) return [];
        var sum = weights.Sum();
        if (sum <= 0) return EqualSplit(count);

        var exact = new double[count];
        var floors = new int[count];
        var assigned = 0;
        for (var i = 0; i < count; i++)
        {
            exact[i] = (double)(weights[i] / sum * 100m);
            floors[i] = (int)Math.Floor(exact[i]);
            assigned += floors[i];
        }

        var remaining = 100 - assigned;
        var order = Enumerable.Range(0, count)
            .OrderByDescending(i => exact[i] - Math.Floor(exact[i]))
            .ThenBy(i => i)
            .ToList();
        for (var k = 0; k < remaining && k < order.Count; k++)
            floors[order[k]]++;

        return floors.Select(v => (decimal)v).ToList();
    }
}

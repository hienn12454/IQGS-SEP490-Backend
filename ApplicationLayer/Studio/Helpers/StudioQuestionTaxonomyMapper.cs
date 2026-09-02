using ApplicationLayer.Studio.Contracts;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// Canonical 3-category taxonomy + styles; legacy questionTypes compatibility.
/// system_design/problem_solving map to technical category + style — never extra top-level categories.
/// </summary>
public static class StudioQuestionTaxonomyMapper
{
    public static readonly IReadOnlySet<string> CanonicalCategories = new HashSet<string>(StringComparer.Ordinal)
    {
        "technical", "behavioral", "situational"
    };

    public static readonly IReadOnlySet<string> CanonicalStyles = new HashSet<string>(StringComparer.Ordinal)
    {
        "system_design", "problem_solving", "debugging", "performance_analysis", "coding", "code_review", "theory"
    };

    public static readonly IReadOnlySet<string> CanonicalCodingTaskTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BUG_DETECTION", "CODE_COMPLETION", "REFACTORING", "TEST_CASE_DESIGN", "PERFORMANCE_ANALYSIS"
    };

    public static string NormalizeCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "technical";
        var key = value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        return key switch
        {
            "system_design" or "systemdesign" => "technical",
            "problem_solving" or "problemsolving" => "technical",
            "behavioral" => "behavioral",
            "situational" => "situational",
            "technical" => "technical",
            _ when CanonicalCategories.Contains(key) => key,
            _ => "technical"
        };
    }

    public static string? NormalizeStyle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var key = value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        if (key is "systemdesign") key = "system_design";
        if (key is "problemsolving") key = "problem_solving";
        if (key is "performance" or "performanceanalysis") key = "performance_analysis";
        if (key is "code_review" or "codereview") key = "code_review";
        return CanonicalStyles.Contains(key) ? key : null;
    }

    public static string NormalizeDifficulty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "medium";
        var key = value.Trim().ToLowerInvariant();
        return key is "easy" or "medium" or "hard" ? key : "medium";
    }

    /// <summary>Legacy type → (category, optional style).</summary>
    public static (string Category, string? Style) LegacyTypeToCanonical(string legacyType)
    {
        var key = legacyType.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        return key switch
        {
            "system_design" or "systemdesign" => ("technical", "system_design"),
            "problem_solving" or "problemsolving" => ("technical", "problem_solving"),
            "behavioral" => ("behavioral", null),
            "situational" => ("situational", null),
            "technical" => ("technical", null),
            _ => ("technical", null)
        };
    }

    /// <summary>Collapse legacy questionTypes list into unique canonical categories (max 3).</summary>
    public static IReadOnlyList<string> ToLegacyQuestionTypes(IReadOnlyList<QuestionDistributionItemDto> distribution)
    {
        var cats = distribution
            .Select(d => NormalizeCategory(d.Category))
            .Distinct(StringComparer.Ordinal)
            .Where(CanonicalCategories.Contains)
            .ToList();
        if (cats.Count == 0) return ["technical", "behavioral"];
        return cats;
    }

    public static string ToQuestionStylesHrNote(IEnumerable<string> styles)
    {
        var normalized = styles
            .Select(NormalizeStyle)
            .Where(s => s is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return normalized.Count == 0 ? string.Empty : $"QUESTION_STYLES={string.Join(",", normalized)}";
    }

    /// <summary>Convert legacy types + total questions into 3-category distribution with deduped styles.</summary>
    public static (List<QuestionDistributionItemDto> Distribution, List<string> Styles) FromLegacyQuestionTypes(
        IReadOnlyList<string> legacyTypes,
        int totalQuestions)
    {
        totalQuestions = Math.Clamp(totalQuestions, 1, 50);
        var types = legacyTypes.Count > 0 ? legacyTypes : StudioQuestionTypesHelper.DefaultTypes;
        var categoryCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["technical"] = 0,
            ["behavioral"] = 0,
            ["situational"] = 0
        };
        var styles = new List<string>();

        foreach (var raw in types)
        {
            var (cat, style) = LegacyTypeToCanonical(raw);
            categoryCounts[cat] = categoryCounts.GetValueOrDefault(cat) + 1;
            if (style is not null && !styles.Contains(style, StringComparer.Ordinal))
                styles.Add(style);
        }

        var active = categoryCounts.Where(kv => kv.Value > 0).ToList();
        if (active.Count == 0)
        {
            active = [new KeyValuePair<string, int>("technical", 1), new KeyValuePair<string, int>("behavioral", 1)];
        }

        var perCat = totalQuestions / active.Count;
        var remainder = totalQuestions % active.Count;
        var dist = new List<QuestionDistributionItemDto>();
        var order = 0;
        foreach (var (cat, _) in active)
        {
            var count = perCat + (order < remainder ? 1 : 0);
            order++;
            var pct = totalQuestions > 0 ? (int)Math.Round(count * 100.0 / totalQuestions) : 0;
            dist.Add(new QuestionDistributionItemDto(cat, pct, count));
        }
        FixDistributionPercentages(dist, totalQuestions);
        return (dist, styles);
    }

    /// <summary>Scale count từng category về đúng totalQuestions (tránh 12/10 trên UI).</summary>
    public static void RescaleQuestionCounts(List<QuestionDistributionItemDto> items, int totalQuestions)
    {
        if (items.Count == 0 || totalQuestions <= 0) return;
        var weights = items.Select(i => Math.Max(0, i.QuestionCount)).ToList();
        var sumW = weights.Sum();
        if (sumW <= 0)
        {
            var even = totalQuestions / items.Count;
            var rem = totalQuestions % items.Count;
            for (var i = 0; i < items.Count; i++)
            {
                var c = even + (i < rem ? 1 : 0);
                var pct = (int)Math.Round(c * 100.0 / totalQuestions);
                items[i] = items[i] with { QuestionCount = c, Percentage = pct };
            }
            FixDistributionPercentages(items, totalQuestions);
            return;
        }

        var raw = weights.Select(w => w / (double)sumW * totalQuestions).ToList();
        var floors = raw.Select(x => (int)Math.Floor(x)).ToList();
        var leftover = totalQuestions - floors.Sum();
        var order = raw
            .Select((x, i) => (i, frac: x - floors[i]))
            .OrderByDescending(t => t.frac)
            .ToList();
        for (var r = 0; r < leftover; r++)
            floors[order[r % order.Count].i] += 1;

        for (var i = 0; i < items.Count; i++)
        {
            var c = Math.Max(0, floors[i]);
            var pct = (int)Math.Round(c * 100.0 / totalQuestions);
            items[i] = items[i] with { QuestionCount = c, Percentage = pct };
        }
        FixDistributionPercentages(items, totalQuestions);
    }

    public static void FixDistributionPercentages(List<QuestionDistributionItemDto> items, int totalQuestions)
    {
        if (items.Count == 0 || totalQuestions <= 0) return;
        var sumPct = items.Sum(i => i.Percentage);
        if (sumPct == 100) return;
        var last = items[^1];
        items[^1] = last with { Percentage = last.Percentage + (100 - sumPct) };
    }

    public static List<string> ExtractStylesFromLegacyTypes(IReadOnlyList<string> legacyTypes)
    {
        var styles = new List<string>();
        foreach (var raw in legacyTypes)
        {
            var (_, style) = LegacyTypeToCanonical(raw);
            if (style is not null && !styles.Contains(style, StringComparer.Ordinal))
                styles.Add(style);
        }
        return styles;
    }
}

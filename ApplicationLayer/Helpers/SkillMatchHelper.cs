using System.Text.Json;

namespace ApplicationLayer.Helpers;

public static class SkillMatchHelper
{
    public static IReadOnlyList<string> UnionCvSkills(IEnumerable<string>? techStack, string? evaluationJson)
        => Normalize(Normalize(techStack).Concat(ParseEvaluationSkills(evaluationJson)));

    public static string? ParseEvaluationSummary(string? evaluationJson)
    {
        if (string.IsNullOrWhiteSpace(evaluationJson))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(evaluationJson);
            if (doc.RootElement.TryGetProperty("summary", out var el) && el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString()?.Trim();
                return string.IsNullOrEmpty(s) ? null : s;
            }
        }
        catch (JsonException)
        {
            /* ignore */
        }
        return null;
    }

    public static IReadOnlyList<string> ParseEvaluationSkills(string? evaluationJson)
    {
        if (string.IsNullOrWhiteSpace(evaluationJson))
            return Array.Empty<string>();
        try
        {
            using var doc = JsonDocument.Parse(evaluationJson);
            if (!doc.RootElement.TryGetProperty("skills", out var skillsEl))
                return Array.Empty<string>();
            return JsonSerializer.Deserialize<List<string>>(skillsEl.GetRawText()) ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    public static IReadOnlyList<string> Normalize(IEnumerable<string>? skills)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var raw in skills ?? Enumerable.Empty<string>())
        {
            var trimmed = raw?.Trim();
            if (string.IsNullOrEmpty(trimmed) || !seen.Add(trimmed))
                continue;
            list.Add(trimmed);
        }
        return list;
    }

    public static SkillMatchResult Compute(IEnumerable<string>? setSkills, IEnumerable<string>? cvSkills)
    {
        var set = Normalize(setSkills);
        var cv = Normalize(cvSkills);
        if (set.Count == 0)
            return new SkillMatchResult(null, new List<string>(), set.ToList());

        var cvSet = new HashSet<string>(cv, StringComparer.OrdinalIgnoreCase);
        var matched = set.Where(s => cvSet.Contains(s)).ToList();
        var missing = set.Where(s => !cvSet.Contains(s)).ToList();
        var percent = (int)Math.Round(100.0 * matched.Count / set.Count);
        return new SkillMatchResult(percent, matched, missing);
    }
}

public readonly record struct SkillMatchResult(int? Percent, List<string> Matched, List<string> Missing);

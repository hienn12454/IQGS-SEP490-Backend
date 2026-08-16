namespace ApplicationLayer.Helpers;

public static class SkillFitCalculator
{
    public static string Normalize(string? skill)
    {
        if (string.IsNullOrWhiteSpace(skill)) return string.Empty;
        var chars = skill.Trim().ToLowerInvariant().ToCharArray();
        var collapsed = new char[chars.Length];
        var n = 0;
        var prevSpace = false;
        foreach (var c in chars)
        {
            var isSpace = char.IsWhiteSpace(c);
            if (isSpace)
            {
                if (prevSpace) continue;
                collapsed[n++] = ' ';
                prevSpace = true;
            }
            else
            {
                collapsed[n++] = c;
                prevSpace = false;
            }
        }
        return new string(collapsed, 0, n);
    }

    public static HashSet<string> ToSet(IEnumerable<string?> skills)
        => skills
            .Select(Normalize)
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

    public static SkillFitResult Compute(IEnumerable<string?> jdSkills, IEnumerable<string?> cvSkills)
    {
        var jd = ToSet(jdSkills);
        var cv = ToSet(cvSkills);
        if (jd.Count == 0 && cv.Count == 0)
            return new SkillFitResult(0, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        var matched = jd.Intersect(cv, StringComparer.Ordinal).OrderBy(x => x).ToList();
        var missing = jd.Except(cv, StringComparer.Ordinal).OrderBy(x => x).ToList();
        var extra = cv.Except(jd, StringComparer.Ordinal).OrderBy(x => x).ToList();
        var union = jd.Count + cv.Count - matched.Count;
        var percent = union == 0 ? 0 : Math.Round(matched.Count * 100.0 / union, 1);
        return new SkillFitResult(percent, matched, missing, extra);
    }
}

public sealed record SkillFitResult(
    double FitPercent,
    IReadOnlyList<string> Matched,
    IReadOnlyList<string> MissingOnCv,
    IReadOnlyList<string> ExtraOnCv);

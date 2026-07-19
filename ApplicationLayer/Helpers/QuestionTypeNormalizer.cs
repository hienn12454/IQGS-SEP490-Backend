namespace ApplicationLayer.Helpers;

public static class QuestionTypeNormalizer
{
    public static readonly IReadOnlySet<string> AllowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "technical",
        "behavioral",
        "situational",
        "system-design",
        "problem-solving"
    };

    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Technical"] = "technical",
        ["Behavioral"] = "behavioral",
        ["Situational"] = "situational",
        ["System Design"] = "system-design",
        ["System-Design"] = "system-design",
        ["System_design"] = "system-design",
        ["SystemDesign"] = "system-design",
        ["system_design"] = "system-design",
        ["Problem-Solving"] = "problem-solving",
        ["Problem_solving"] = "problem-solving",
        ["ProblemSolving"] = "problem-solving",
        ["problem_solving"] = "problem-solving",
        ["Follow-up"] = "follow-up",
        ["Follow_up"] = "follow-up",
        ["FollowUp"] = "follow-up",
        ["technical"] = "technical",
        ["behavioral"] = "behavioral",
        ["situational"] = "situational",
        ["system-design"] = "system-design",
        ["problem-solving"] = "problem-solving",
        ["follow-up"] = "follow-up"
    };

    public static IReadOnlyList<string> Normalize(IEnumerable<string> questionTypes)
    {
        return questionTypes
            .Select(NormalizeOne)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Chuẩn hóa 1 type: map alias đã biết; còn lại đổi underscore → hyphen rồi lower-case
    /// để tránh tách "problem_solving" và "problem-solving" thành 2 bucket trên dashboard.
    /// </summary>
    public static string NormalizeOne(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "technical";

        var trimmed = raw.Trim();
        if (Map.TryGetValue(trimmed, out var mapped))
            return mapped;

        // Compact key không dấu phân cách (SystemDesign → systemdesign)
        var compact = new string(trimmed.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        foreach (var kv in Map)
        {
            var mapCompact = new string(kv.Key.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            if (mapCompact == compact)
                return kv.Value;
        }

        return trimmed.Replace('_', '-').Replace(' ', '-').ToLowerInvariant();
    }

    public static bool IsAllowed(string normalizedType)
        => AllowedTypes.Contains(normalizedType);
}

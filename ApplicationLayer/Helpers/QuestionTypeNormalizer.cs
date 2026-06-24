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
        ["Problem-Solving"] = "problem-solving",
        ["technical"] = "technical",
        ["behavioral"] = "behavioral",
        ["situational"] = "situational",
        ["system-design"] = "system-design",
        ["problem-solving"] = "problem-solving"
    };

    public static IReadOnlyList<string> Normalize(IEnumerable<string> questionTypes)
    {
        return questionTypes
            .Select(t => Map.TryGetValue(t.Trim(), out var normalized) ? normalized : t.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    public static bool IsAllowed(string normalizedType)
        => AllowedTypes.Contains(normalizedType);
}

using System.Text.Json;
using DomainLayer.Studio;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>SCRUM-370: Parse/validate QuestionTypes trên StudioSettings.</summary>
public static class StudioQuestionTypesHelper
{
    public static readonly string[] Allowed =
    [
        "technical",
        "system_design",
        "problem_solving",
        "behavioral",
        "situational"
    ];

    public static readonly IReadOnlyList<string> DefaultTypes =
    [
        "technical",
        "system_design",
        "problem_solving",
        "behavioral"
    ];

    public static List<string> ParseOrDefault(string? json)
    {
        var parsed = Parse(json);
        return parsed.Count > 0 ? parsed : DefaultTypes.ToList();
    }

    public static List<string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return Normalize(list);
        }
        catch
        {
            return [];
        }
    }

    public static List<string> Normalize(IEnumerable<string>? raw)
    {
        if (raw is null) return [];
        var result = new List<string>();
        foreach (var item in raw)
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            var key = item.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
            if (key is "systemdesign") key = "system_design";
            if (key is "problemsolving") key = "problem_solving";
            if (!Allowed.Contains(key)) continue;
            if (!result.Contains(key, StringComparer.Ordinal))
                result.Add(key);
        }
        return result;
    }

    public static string ToJson(IEnumerable<string> types)
    {
        var normalized = Normalize(types);
        if (normalized.Count == 0) normalized = DefaultTypes.ToList();
        return JsonSerializer.Serialize(normalized);
    }

    public static void EnsureValidOrThrow(IEnumerable<string>? types)
    {
        var normalized = Normalize(types);
        if (normalized.Count == 0)
            throw new StudioBusinessException(
                "QUESTION_TYPES_REQUIRED",
                StatusCodes.Status400BadRequest,
                "Cần chọn ít nhất 1 loại câu hỏi hợp lệ.");
    }
}

using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ApplicationLayer.Helpers;

/// <summary>
/// Folder SYSTEM dùng khi sinh đề Coach (diagnostic / drill / reassess).
/// Config: Coach:DiagnosticKnowledgeFolder — mặc định test-candidate.
/// </summary>
public static class CoachDiagnosticKnowledgeFolder
{
    public const string DefaultFolder = "test-candidate";
    public const string ConfigKey = "Coach:DiagnosticKnowledgeFolder";

    public const string KbSourceSystem = "system";
    public const string KbSourceInferred = "inferred";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Đọc và normalize folder từ config; rỗng/invalid → default.</summary>
    public static string Resolve(IConfiguration? config)
    {
        var raw = config?[ConfigKey];
        try
        {
            var normalized = KnowledgeFolderHelper.Normalize(raw);
            return normalized ?? DefaultFolder;
        }
        catch
        {
            return DefaultFolder;
        }
    }

    /// <summary>Serialize provenance vào GapSkillsJson (coach jobs).</summary>
    public static string SerializeKbSource(string kbSource)
    {
        var normalized = string.Equals(kbSource, KbSourceSystem, StringComparison.OrdinalIgnoreCase)
            ? KbSourceSystem
            : KbSourceInferred;
        return JsonSerializer.Serialize(new { kbSource = normalized }, JsonOpts);
    }

    /// <summary>Đọc kbSource từ GapSkillsJson dạng object; mảng/rỗng → null.</summary>
    public static string? ParseKbSourceFromGapSkillsJson(string? gapSkillsJson)
    {
        if (string.IsNullOrWhiteSpace(gapSkillsJson) || gapSkillsJson is "[]" or "{}")
            return null;
        try
        {
            using var doc = JsonDocument.Parse(gapSkillsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            if (doc.RootElement.TryGetProperty("kbSource", out var ks) ||
                doc.RootElement.TryGetProperty("KbSource", out ks))
            {
                var v = ks.GetString();
                if (string.Equals(v, KbSourceSystem, StringComparison.OrdinalIgnoreCase))
                    return KbSourceSystem;
                if (string.Equals(v, KbSourceInferred, StringComparison.OrdinalIgnoreCase))
                    return KbSourceInferred;
                return v;
            }
        }
        catch (JsonException)
        {
            /* ignore */
        }

        return null;
    }
}

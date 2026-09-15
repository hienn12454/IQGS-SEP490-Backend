using System.Text.Json;
using DomainLayer.Constants;

namespace ApplicationLayer.DTOs.Coach;

/// <summary>
/// SCRUM-457: hợp đồng chung FRAMEWORK + ADAPTIVE. Downstream chỉ đọc object này.
/// </summary>
public class CompetencyBlueprint
{
    public int SchemaVersion { get; set; } = CompetencyBlueprintSchema.CurrentVersion;
    public Guid BlueprintId { get; set; } = Guid.NewGuid();
    public string SourceMode { get; set; } = CompetencyResolutionMode.Framework;
    public string RoleKey { get; set; } = string.Empty;
    public string TargetRole { get; set; } = string.Empty;
    public string TargetLevel { get; set; } = CoachSeniorityLevel.Junior;
    public string? FrameworkKey { get; set; }
    public Guid? FrameworkId { get; set; }
    public List<CompetencyItem> Competencies { get; set; } = new();
    public DiagnosticDistribution DiagnosticDistribution { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CompetencyItem
{
    public string SkillKey { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
    public string Category { get; set; } = CompetencyCategory.RoleCore;
    public double Weight { get; set; }
    public string ExpectedLevel { get; set; } = CoachSeniorityLevel.Junior;
    public double TargetScore { get; set; }
    public List<string> Topics { get; set; } = new();
    public string Source { get; set; } = CompetencySourceMode.Framework;
    public List<CompetencyCitation> Citations { get; set; } = new();
}

public class CompetencyCitation
{
    public string? SourceTitle { get; set; }
    public string? SourceUrl { get; set; }
    public string? Section { get; set; }
    public Guid? DocumentId { get; set; }
    public string? Excerpt { get; set; }
}

public class DiagnosticDistribution
{
    public int Fundamental { get; set; } = 30;
    public int RoleCore { get; set; } = 40;
    public int Advanced { get; set; } = 30;
}

public static class CompetencyBlueprintJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(CompetencyBlueprint blueprint)
        => JsonSerializer.Serialize(blueprint, Options);

    public static CompetencyBlueprint? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<CompetencyBlueprint>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

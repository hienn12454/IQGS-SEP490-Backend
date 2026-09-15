namespace ApplicationLayer.DTOs.Coach;

/// <summary>
/// SCRUM-453: payload import competency framework từ curated data.
/// Một file = một RoleKey, nhiều level. Không có field nào riêng cho stack cụ thể.
/// </summary>
public class CompetencyFrameworkImportDto
{
    public string RoleKey { get; set; } = string.Empty;
    public string DisplayRole { get; set; } = string.Empty;
    public string? Technology { get; set; }
    public List<string> Stack { get; set; } = new();
    /// <summary>Cách gọi khác của vai trò này (dùng để resolve target role tự do).</summary>
    public List<CompetencyRoleAliasImportDto> Aliases { get; set; } = new();
    public List<CompetencyFrameworkLevelImportDto> Levels { get; set; } = new();
    /// <summary>Nguồn curated data (tên tài liệu/repo) — bắt buộc để truy vết số liệu.</summary>
    public string SourceRef { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CompetencyRoleAliasImportDto
{
    public string Alias { get; set; } = string.Empty;
    /// <summary>exact | contains (default contains).</summary>
    public string? MatchKind { get; set; }
}

public class CompetencyFrameworkLevelImportDto
{
    public string Level { get; set; } = string.Empty;
    /// <summary>Active | Draft (default Active).</summary>
    public string? Status { get; set; }
    public List<CompetencyFrameworkSkillImportDto> Skills { get; set; } = new();
}

public class CompetencyFrameworkSkillImportDto
{
    public string Skill { get; set; } = string.Empty;
    public double ImportanceWeight { get; set; }
    public double TargetScore { get; set; }
    /// <summary>easy | medium | hard.</summary>
    public string RequiredDifficulty { get; set; } = string.Empty;
    public List<CompetencyTopicImportDto> Topics { get; set; } = new();
}

public class CompetencyTopicImportDto
{
    public string Topic { get; set; } = string.Empty;
    public List<string> Subtopics { get; set; } = new();
}

public class CompetencyFrameworkImportResultDto
{
    public bool Success { get; set; }
    public bool DryRun { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<CompetencyFrameworkImportedLevelDto> Imported { get; set; } = new();
    public int AliasCount { get; set; }
}

public class CompetencyFrameworkImportedLevelDto
{
    public Guid? FrameworkId { get; set; }
    public string RoleKey { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public int SkillCount { get; set; }
}

public class CompetencyFrameworkAdminDto
{
    public Guid Id { get; set; }
    public string RoleKey { get; set; } = string.Empty;
    public string DisplayRole { get; set; } = string.Empty;
    public string? Technology { get; set; }
    public string TargetLevel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Provenance { get; set; } = string.Empty;
    public string? SourceRef { get; set; }
    public string? SourceVersion { get; set; }
    public int SkillCount { get; set; }
    public double WeightSum { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpdateCompetencyFrameworkStatusDto
{
    /// <summary>Active | Draft.</summary>
    public string Status { get; set; } = string.Empty;
}

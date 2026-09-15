namespace ApplicationLayer.DTOs.Coach;

public class CompetencyRoleFamilyDto
{
    public Guid Id { get; set; }
    public string FamilyKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Description { get; set; }
    public List<CompetencyRoleFamilyAliasDto> Aliases { get; set; } = new();
}

public class CompetencyRoleFamilyAliasDto
{
    public Guid? Id { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string MatchKind { get; set; } = "contains";
    public int SortOrder { get; set; }
}

public class UpsertCompetencyRoleFamilyDto
{
    public string FamilyKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public int SortOrder { get; set; }
    public string? Description { get; set; }
    public List<CompetencyRoleFamilyAliasDto> Aliases { get; set; } = new();
}

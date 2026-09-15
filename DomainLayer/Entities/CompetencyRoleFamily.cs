namespace DomainLayer.Entities;

/// <summary>
/// SCRUM-457: nhóm vai trò software engineering (data/config).
/// Resolver phân ADAPTIVE vs UNSUPPORTED dựa bảng này — không hardcode list trong code.
/// </summary>
public class CompetencyRoleFamily : BaseEntity
{
    public string FamilyKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = Constants.CompetencyFrameworkStatus.Active;
    public int SortOrder { get; set; }
    public string? Description { get; set; }

    public ICollection<CompetencyRoleFamilyAlias> Aliases { get; set; } = new List<CompetencyRoleFamilyAlias>();
}

/// <summary>Cách gọi khác của một role family (vd "be" → family backend). Unique toàn hệ thống.</summary>
public class CompetencyRoleFamilyAlias : BaseEntity
{
    public string FamilyKey { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string MatchKind { get; set; } = Constants.CompetencyRoleAliasMatchKind.Contains;
    public int SortOrder { get; set; }
}

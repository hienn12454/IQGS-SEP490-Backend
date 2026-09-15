namespace DomainLayer.Entities;

/// <summary>
/// Catalog competency theo Role + Technology + Target Level (vd .NET Backend / ASP.NET Core / Junior).
/// SCRUM-453: đây là DATA thuần — thêm Java/Python/React chỉ cần import thêm row,
/// không sửa scoring engine hay thêm nhánh if theo stack.
/// </summary>
public class CompetencyFramework : BaseEntity
{
    public string RoleKey { get; set; } = string.Empty;
    public string DisplayRole { get; set; } = string.Empty;
    public string TargetLevel { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string? Description { get; set; }

    /// <summary>Công nghệ chính (vd "ASP.NET Core", "Spring Boot", "React") — hiển thị + filter roadmap node.</summary>
    public string? Technology { get; set; }
    /// <summary>JSON mảng công nghệ phụ trong stack (vd ["EF Core","PostgreSQL"]).</summary>
    public string StackJson { get; set; } = "[]";
    /// <summary>Provisional (seed MVP) | Curated (import từ bộ data đã thẩm định).</summary>
    public string Provenance { get; set; } = Constants.CompetencyFrameworkProvenance.Provisional;
    /// <summary>Tên/đường dẫn bộ curated data đã import — để truy vết số liệu từ đâu.</summary>
    public string? SourceRef { get; set; }
    public string? SourceVersion { get; set; }

    /// <summary>SCRUM-457: family SE (backend/frontend/...) — data, không hardcode. Null = chưa gán.</summary>
    public string? RoleFamilyKey { get; set; }

    public ICollection<CompetencyFrameworkSkill> Skills { get; set; } = new List<CompetencyFrameworkSkill>();
}

/// <summary>
/// Alias vai trò để resolve framework từ text tự do của ứng viên ("ASP.NET Core Developer" -> dotnet-backend).
/// Là DATA: thêm role mới bằng import alias, không hardcode trong repository/service.
/// </summary>
public class CompetencyRoleAlias : BaseEntity
{
    public string RoleKey { get; set; } = string.Empty;
    /// <summary>Alias đã normalize (lowercase, trim).</summary>
    public string Alias { get; set; } = string.Empty;
    /// <summary>exact | contains — cách so khớp với target role người dùng nhập.</summary>
    public string MatchKind { get; set; } = Constants.CompetencyRoleAliasMatchKind.Contains;
    public int SortOrder { get; set; }
}

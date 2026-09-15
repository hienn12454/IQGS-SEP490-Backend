namespace DomainLayer.Entities;

/// <summary>
/// SCRUM-455: node kiến thức lộ trình (curated). Đây là DATA — thêm role/stack mới bằng import JSONL,
/// không sửa scoring engine. RoleKey phải khớp catalog framework; node lệch catalog bị cảnh báo khi import.
/// </summary>
public class RoadmapNode : BaseEntity
{
    public string RoleKey { get; set; } = string.Empty;
    public string? Technology { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Skill { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string? Subtopic { get; set; }
    public double Importance { get; set; } = 0.5;
    public string PrerequisitesJson { get; set; } = "[]";
    public string NextTopicsJson { get; set; } = "[]";
    public string? SourceTitle { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceVersion { get; set; }
    public Guid? KnowledgeDocumentId { get; set; }
    public int SortOrder { get; set; }
}

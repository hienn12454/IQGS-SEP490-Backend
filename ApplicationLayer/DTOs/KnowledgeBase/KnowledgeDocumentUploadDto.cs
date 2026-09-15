namespace ApplicationLayer.DTOs.KnowledgeBase;

public class KnowledgeDocumentUploadDto
{
    public string Scope { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }

    /// <summary>
    /// SCRUM-442/447: Policy | InternalStack | Rubric | RolePack | Roadmap.
    /// HR bắt buộc; Admin tùy chọn (null → Unclassified).
    /// Lưu vào cột section.
    /// </summary>
    public string? DocumentType { get; set; }

    /// <summary>SCRUM-447: ghi chú Admin khi upload SYSTEM.</summary>
    public string? AdminNote { get; set; }

    /// <summary>SCRUM-450: nhóm folder UI (vd. swe). Chỉ SYSTEM/Admin.</summary>
    public string? Folder { get; set; }
}

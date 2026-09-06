namespace ApplicationLayer.DTOs.KnowledgeBase;

public class KnowledgeDocumentUploadDto
{
    public string Scope { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }

    /// <summary>
    /// SCRUM-442: Policy | InternalStack | Rubric | RolePack.
    /// HR bắt buộc; Admin tùy chọn (null → Unclassified).
    /// Lưu vào cột section.
    /// </summary>
    public string? DocumentType { get; set; }
}

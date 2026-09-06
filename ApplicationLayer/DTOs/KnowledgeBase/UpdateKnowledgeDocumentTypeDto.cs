namespace ApplicationLayer.DTOs.KnowledgeBase;

/// <summary>SCRUM-442: PATCH đổi loại tài liệu (section).</summary>
public class UpdateKnowledgeDocumentTypeDto
{
    /// <summary>Policy | InternalStack | Rubric | RolePack (HR); Unclassified chỉ Admin.</summary>
    public string DocumentType { get; set; } = string.Empty;
}

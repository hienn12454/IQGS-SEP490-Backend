namespace ApplicationLayer.DTOs.KnowledgeBase;

/// <summary>SCRUM-447: Admin PATCH type + ghi chú nội bộ (không clone Storage Explorer).</summary>
public class UpdateKnowledgeDocumentMetaDto
{
    /// <summary>Policy | InternalStack | Rubric | RolePack | Roadmap | Unclassified</summary>
    public string? DocumentType { get; set; }

    /// <summary>Ghi chú Admin — tối đa 2000 ký tự.</summary>
    public string? AdminNote { get; set; }

    /// <summary>SCRUM-450: đổi nhóm folder (null/empty + ClearFolder = unsorted).</summary>
    public string? Folder { get; set; }

    /// <summary>true = gán Folder=null (unsorted), kể cả khi Folder null.</summary>
    public bool ClearFolder { get; set; }
}

namespace ApplicationLayer.DTOs.KnowledgeBase;

/// <summary>SCRUM-451: đổi tên folder (metadata) — gán lại Folder cho mọi doc cùng scope.</summary>
public class RenameKnowledgeFolderDto
{
    public string From { get; set; } = string.Empty;
    /// <summary>null/empty hoặc "unsorted" → gỡ folder (unsorted).</summary>
    public string? To { get; set; }
}

/// <summary>SCRUM-451: chuyển nhiều document sang một folder.</summary>
public class MoveKnowledgeDocumentsDto
{
    public List<Guid> DocumentIds { get; set; } = [];
    /// <summary>null/empty → unsorted.</summary>
    public string? Folder { get; set; }
}

public class FolderMutationResultDto
{
    public int UpdatedCount { get; set; }
}

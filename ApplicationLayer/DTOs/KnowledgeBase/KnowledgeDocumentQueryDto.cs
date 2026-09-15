namespace ApplicationLayer.DTOs.KnowledgeBase;

/// <summary>Query nội bộ repository — scope/owner do controller/service gán.</summary>
public class KnowledgeDocumentQueryDto
{
    public string? Scope { get; set; }
    public Guid? OwnerId { get; set; }
    public string? FileName { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>SCRUM-450: lọc folder; "unsorted" = null/empty.</summary>
    public string? Folder { get; set; }
}

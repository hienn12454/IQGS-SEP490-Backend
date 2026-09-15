namespace ApplicationLayer.DTOs.KnowledgeBase;

public class KnowledgeFolderDto
{
    /// <summary>Tên folder; "unsorted" = chưa gán.</summary>
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

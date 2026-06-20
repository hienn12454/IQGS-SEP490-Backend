namespace ApplicationLayer.DTOs.KnowledgeBase;

public class UpdateKnowledgeDocumentStatusDto
{
    public string Status { get; set; } = string.Empty;
    public int? ChunkCount { get; set; }
    public string? ErrorMessage { get; set; }
}

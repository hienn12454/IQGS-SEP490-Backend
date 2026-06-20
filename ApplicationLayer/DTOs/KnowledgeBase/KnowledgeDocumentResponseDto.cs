namespace ApplicationLayer.DTOs.KnowledgeBase;

public class KnowledgeDocumentResponseDto
{
    public Guid DocumentId { get; set; }
    public string Scope { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? ChunkCount { get; set; }
    public Guid UploadedBy { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

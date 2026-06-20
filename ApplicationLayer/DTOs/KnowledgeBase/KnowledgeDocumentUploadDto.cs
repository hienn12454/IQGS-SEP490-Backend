namespace ApplicationLayer.DTOs.KnowledgeBase;

public class KnowledgeDocumentUploadDto
{
    public string Scope { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
}

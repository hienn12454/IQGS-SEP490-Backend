namespace ApplicationLayer.DTOs.KnowledgeBase;

/// <summary>SCRUM-444: preview chunk từ tbl_knowledge_chunks.</summary>
public class KnowledgeChunkPreviewDto
{
    public Guid ChunkId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
}

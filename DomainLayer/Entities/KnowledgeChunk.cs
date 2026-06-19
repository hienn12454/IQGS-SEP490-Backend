using Pgvector;

namespace DomainLayer.Entities;

/// <summary>
/// Chunk embedding — RAG service ghi trực tiếp vào PostgreSQL pgvector.
/// Backend chỉ định nghĩa schema để migration và shared DB.
/// </summary>
public class KnowledgeChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Guid? OwnerId { get; set; }
    public string Scope { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public Vector? Embedding { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public KnowledgeDocument Document { get; set; } = null!;
}

namespace DomainLayer.Entities;

/// <summary>
/// Metadata tài liệu knowledge base — file thật nằm trên Azure Blob.
/// </summary>
public class KnowledgeDocument : BaseEntity
{
    public string Scope { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;
    public string? ContentHash { get; set; }
    public string? SourceTitle { get; set; }
    public string? SourceUrl { get; set; }
    public string? Section { get; set; }
    public int? Year { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ChunkCount { get; set; }
    public Guid UploadedBy { get; set; }
    public string? ErrorMessage { get; set; }

    public ICollection<KnowledgeChunk> Chunks { get; set; } = new List<KnowledgeChunk>();
}

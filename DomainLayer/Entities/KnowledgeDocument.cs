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

    /// <summary>SCRUM-447: ghi chú Admin trên từng file KB (không lưu Blob metadata).</summary>
    public string? AdminNote { get; set; }

    /// <summary>SCRUM-450: nhóm folder ảo trên UI Admin (vd. swe). Không đổi Blob path.</summary>
    public string? Folder { get; set; }

    public ICollection<KnowledgeChunk> Chunks { get; set; } = new List<KnowledgeChunk>();
}

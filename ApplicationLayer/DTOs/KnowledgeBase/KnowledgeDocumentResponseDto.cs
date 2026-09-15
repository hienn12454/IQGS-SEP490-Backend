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

    /// <summary>SCRUM-442/447: Policy | InternalStack | Rubric | RolePack | Roadmap | Unclassified.</summary>
    public string DocumentType { get; set; } = "Unclassified";

    /// <summary>SCRUM-447: ghi chú Admin cho kho SYSTEM.</summary>
    public string? AdminNote { get; set; }

    /// <summary>SCRUM-450: nhóm folder UI (null = unsorted).</summary>
    public string? Folder { get; set; }

    /// <summary>Đường dẫn blob (để FE hiển thị path ảo, không list container).</summary>
    public string? BlobPath { get; set; }

    /// <summary>SCRUM-444: số project Studio đang gắn (active).</summary>
    public int StudioProjectCount { get; set; }

    /// <summary>SCRUM-444: số lần cite theo sourceFile ≈ FileName (ước lượng).</summary>
    public int CitationCount { get; set; }
}

namespace ApplicationLayer.DTOs.Candidate;

/// <summary>SCRUM-486: payload xem tài liệu nguồn trên Coach roadmap.</summary>
public class CoachKnowledgeViewDto
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    /// <summary>markdown | text | pdf | docx</summary>
    public string ContentType { get; set; } = string.Empty;
    /// <summary>Nội dung text (md/txt); null với pdf/docx.</summary>
    public string? Content { get; set; }
    /// <summary>SAS URL ngắn hạn (pdf/docx).</summary>
    public string? Url { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? SourceTitle { get; set; }
    /// <summary>Preview text từ chunks (docx fallback).</summary>
    public string? PreviewText { get; set; }
}

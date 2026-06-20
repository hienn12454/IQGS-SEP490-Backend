namespace ApplicationLayer.DTOs.KnowledgeBase;

/// <summary>Query list tài liệu — FE chỉ cần pagination + lọc tên file / ngày nhập.</summary>
public class KnowledgeDocumentListQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Tìm theo tên file (contains, không phân biệt hoa thường). Bỏ trống = tất cả.</summary>
    public string? FileName { get; set; }

    /// <summary>Lọc từ ngày nhập (createdAt), inclusive.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Lọc đến ngày nhập (createdAt), inclusive.</summary>
    public DateTime? ToDate { get; set; }
}

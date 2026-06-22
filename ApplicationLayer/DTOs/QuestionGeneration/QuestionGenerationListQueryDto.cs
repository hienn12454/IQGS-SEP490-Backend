namespace ApplicationLayer.DTOs.QuestionGeneration;

/// <summary>Query danh sách generation session của HR — pagination + lọc status/ngày.</summary>
public class QuestionGenerationListQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Lọc theo status chính xác (PLAN_QUEUED, COMPLETED, ...). Bỏ trống = tất cả.</summary>
    public string? Status { get; set; }

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

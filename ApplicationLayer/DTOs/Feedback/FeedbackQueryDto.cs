namespace ApplicationLayer.DTOs.Feedback;

public class FeedbackQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    /// <summary>Lọc theo trạng thái: Pending | Approved | Rejected. null = tất cả.</summary>
    public string? Status { get; set; }

    /// <summary>Lọc theo vai trò người gửi: HR | Candidate. null = tất cả.</summary>
    public string? Role { get; set; }

    /// <summary>Tìm theo nội dung feedback hoặc tên người gửi.</summary>
    public string? Search { get; set; }
}

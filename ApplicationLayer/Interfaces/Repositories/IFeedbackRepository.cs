using ApplicationLayer.DTOs.Feedback;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IFeedbackRepository : IBaseRepository<Feedback>
{
    /// <summary>Feedback đã duyệt, dùng cho landing page (public).</summary>
    Task<List<Feedback>> GetApprovedAsync(int limit);

    /// <summary>Danh sách feedback phân trang cho Admin, kèm bộ lọc.</summary>
    Task<(List<Feedback> Items, int TotalCount)> GetPagedAsync(FeedbackQueryDto query);
}

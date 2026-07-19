using ApplicationLayer.DTOs.Recommendation;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

/// <summary>Đọc/ghi candidate_recommendations (SCRUM-291 / SCRUM-328).</summary>
public interface ICandidateRecommendationRepository
{
    Task<CandidateRecommendation?> GetByIdAsync(Guid id);

    Task<CandidateRecommendation?> GetByCandidateAndSetAsync(Guid candidateUserId, Guid questionSetId);

    Task AddAsync(CandidateRecommendation recommendation);

    Task UpdateAsync(CandidateRecommendation recommendation);

    /// <summary>Bộ câu hỏi đang PUBLISHED + active — null nếu không thỏa (dùng khi evaluate rule tạo recommendation).</summary>
    Task<QuestionSet?> GetPublishedQuestionSetAsync(Guid questionSetId);

    /// <summary>Dashboard HR: recommendation thuộc HR này kèm thông tin candidate + bộ câu hỏi + trạng thái lời mời.</summary>
    Task<(IReadOnlyList<HrRecommendationRow> Items, int TotalCount)> ListByHrAsync(
        Guid hrOwnerId,
        int page,
        int pageSize,
        string? status,
        Guid? questionSetId,
        double? minScore = null,
        string sortBy = "score",
        string sortDir = "desc");

    /// <summary>1 dòng recommendation cho HR sở hữu — null nếu không tồn tại / không thuộc HR / inactive.</summary>
    Task<HrRecommendationRow?> GetRowByIdForHrAsync(Guid id, Guid hrOwnerId);

    /// <summary>
    /// Chi tiết recommendation cho HR (SCRUM-377) — cùng ownership check như GetRowByIdForHrAsync
    /// nhưng kèm avatar/bio/social/CV meta từ CandidateProfile + User.
    /// </summary>
    Task<HrRecommendationRow?> GetDetailRowByIdForHrAsync(Guid id, Guid hrOwnerId);
}

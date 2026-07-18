using ApplicationLayer.DTOs.Recommendation;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

/// <summary>Đọc/ghi candidate_recommendations (SCRUM-291).</summary>
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
        Guid hrOwnerId, int page, int pageSize, string? status, Guid? questionSetId);
}

using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.Recommendation;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Services;

public interface IRecommendationService
{
    /// <summary>
    /// Evaluate rule MVP sau khi 1 phiên luyện tập COMPLETED:
    /// overallScore ≥ 70 AND allowRecruiterRecommendation AND questionSet.status == PUBLISHED
    /// → tạo/cập nhật candidate_recommendations cho HR sở hữu bộ. Không thỏa rule thì bỏ qua im lặng.
    /// </summary>
    Task GenerateForCompletedSessionAsync(PracticeSession session);

    Task<PagedResultDto<HrRecommendationListItemDto>> ListForHrAsync(Guid hrUserId, HrRecommendationListQueryDto query);

    Task<RecommendationActionResponseDto> ShortlistAsync(Guid id, Guid hrUserId);

    Task<RecommendationActionResponseDto> DismissAsync(Guid id, Guid hrUserId);

    Task<InviteCandidateResponseDto> InviteAsync(Guid id, Guid hrUserId, InviteCandidateRequestDto dto);
}

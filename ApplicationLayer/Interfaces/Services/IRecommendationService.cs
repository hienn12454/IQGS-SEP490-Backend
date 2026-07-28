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

    /// <summary>Chi tiết 1 recommendation thuộc HR hiện tại — kèm profile/CV/stats (SCRUM-377).</summary>
    Task<HrRecommendationDetailDto> GetByIdForHrAsync(Guid id, Guid hrUserId);

    /// <summary>SAS URL tải CV của candidate thuộc recommendation — chỉ HR owner (SCRUM-377).</summary>
    Task<HrRecommendationCvDto> GetCvForHrAsync(Guid id, Guid hrUserId);

    Task<RecommendationActionResponseDto> ShortlistAsync(Guid id, Guid hrUserId);

    Task<RecommendationActionResponseDto> DismissAsync(Guid id, Guid hrUserId);

    Task<InviteCandidateResponseDto> InviteAsync(Guid id, Guid hrUserId, InviteCandidateRequestDto dto);
}

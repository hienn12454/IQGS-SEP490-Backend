using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.Recommendation;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

/// <summary>Rule-based candidate recommendation + thao tác dashboard HR (SCRUM-291, SCRUM-295).</summary>
public class RecommendationService : IRecommendationService
{
    /// <summary>Ngưỡng điểm tối thiểu (thang 0–100) để tạo recommendation — rule MVP SCRUM-288.</summary>
    public const double MinScoreForRecommendation = 70;

    private static readonly string[] FilterableStatuses =
    {
        CandidateRecommendationStatus.New,
        CandidateRecommendationStatus.Shortlisted,
        CandidateRecommendationStatus.Dismissed,
        CandidateRecommendationStatus.Invited
    };

    private readonly ICandidateRecommendationRepository _recommendationRepository;
    private readonly ICandidateInvitationRepository _invitationRepository;
    private readonly ICandidateProfileRepository _profileRepository;

    public RecommendationService(
        ICandidateRecommendationRepository recommendationRepository,
        ICandidateInvitationRepository invitationRepository,
        ICandidateProfileRepository profileRepository)
    {
        _recommendationRepository = recommendationRepository;
        _invitationRepository = invitationRepository;
        _profileRepository = profileRepository;
    }

    public async Task GenerateForCompletedSessionAsync(PracticeSession session)
    {
        if (session.Status != PracticeSessionStatus.Completed)
            return;

        if (session.OverallScore is not double score || score < MinScoreForRecommendation)
            return;

        var profile = await _profileRepository.GetByUserIdAsync(session.CandidateUserId);
        if (profile is null || !profile.AllowRecruiterRecommendation)
            return;

        var questionSet = await _recommendationRepository.GetPublishedQuestionSetAsync(session.QuestionSetId);
        if (questionSet is null)
            return;

        var existing = await _recommendationRepository.GetByCandidateAndSetAsync(
            session.CandidateUserId, session.QuestionSetId);

        if (existing is null)
        {
            await _recommendationRepository.AddAsync(new CandidateRecommendation
            {
                CandidateUserId = session.CandidateUserId,
                QuestionSetId = session.QuestionSetId,
                PracticeSessionId = session.Id,
                HrOwnerId = questionSet.OwnerId,
                OverallScore = score
            });
            return;
        }

        // Làm lại nhiều lần: chỉ giữ điểm cao nhất, không reset trạng thái HR đã xử lý.
        if (score > existing.OverallScore)
        {
            existing.OverallScore = score;
            existing.PracticeSessionId = session.Id;
            existing.UpdatedAt = DateTime.UtcNow;
            await _recommendationRepository.UpdateAsync(existing);
        }
    }

    public async Task<PagedResultDto<HrRecommendationListItemDto>> ListForHrAsync(
        Guid hrUserId, HrRecommendationListQueryDto query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        string? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            status = query.Status.Trim().ToUpperInvariant();
            if (!FilterableStatuses.Contains(status))
                throw new BadRequestException(
                    $"Status không hợp lệ. Giá trị cho phép: {string.Join(", ", FilterableStatuses)}.");
        }

        var (rows, totalCount) = await _recommendationRepository.ListByHrAsync(
            hrUserId, page, pageSize, status, query.QuestionSetId);

        return new PagedResultDto<HrRecommendationListItemDto>
        {
            Items = rows.Select(r => new HrRecommendationListItemDto
            {
                Id = r.Id,
                CandidateUserId = r.CandidateUserId,
                CandidateName = r.CandidateName,
                CandidateEmail = r.CandidateEmail,
                TargetRole = r.TargetRole,
                SeniorityLevel = r.SeniorityLevel,
                TechStack = r.TechStack.ToList(),
                QuestionSetId = r.QuestionSetId,
                QuestionSetTitle = r.QuestionSetTitle ?? string.Empty,
                OverallScore = r.OverallScore,
                Status = r.Status,
                InvitationStatus = r.InvitationStatus,
                RecommendedAt = r.RecommendedAt
            }).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<RecommendationActionResponseDto> ShortlistAsync(Guid id, Guid hrUserId)
    {
        var recommendation = await GetOwnedRecommendationAsync(id, hrUserId);

        if (recommendation.Status == CandidateRecommendationStatus.Invited)
            throw new ConflictException("Recommendation đã gửi lời mời — không thể đổi trạng thái.");

        recommendation.Status = CandidateRecommendationStatus.Shortlisted;
        recommendation.UpdatedAt = DateTime.UtcNow;
        await _recommendationRepository.UpdateAsync(recommendation);

        return new RecommendationActionResponseDto { Id = recommendation.Id, Status = recommendation.Status };
    }

    public async Task<RecommendationActionResponseDto> DismissAsync(Guid id, Guid hrUserId)
    {
        var recommendation = await GetOwnedRecommendationAsync(id, hrUserId);

        if (recommendation.Status == CandidateRecommendationStatus.Invited)
            throw new ConflictException("Recommendation đã gửi lời mời — không thể đổi trạng thái.");

        recommendation.Status = CandidateRecommendationStatus.Dismissed;
        recommendation.UpdatedAt = DateTime.UtcNow;
        await _recommendationRepository.UpdateAsync(recommendation);

        return new RecommendationActionResponseDto { Id = recommendation.Id, Status = recommendation.Status };
    }

    public async Task<InviteCandidateResponseDto> InviteAsync(Guid id, Guid hrUserId, InviteCandidateRequestDto dto)
    {
        var recommendation = await GetOwnedRecommendationAsync(id, hrUserId);

        // Check trực tiếp bảng invitation thay vì chỉ tin vào Status — tránh đâm vào unique index (500)
        // nếu lần mời trước đã tạo invitation nhưng cập nhật Status thất bại giữa chừng.
        if (recommendation.Status == CandidateRecommendationStatus.Invited
            || await _invitationRepository.GetByRecommendationIdAsync(recommendation.Id) is not null)
            throw new ConflictException("Recommendation này đã gửi lời mời trước đó.");

        if (recommendation.Status == CandidateRecommendationStatus.Dismissed)
            throw new ConflictException("Recommendation đã bị dismiss — shortlist lại trước khi mời.");

        var invitation = new CandidateInvitation
        {
            RecommendationId = recommendation.Id,
            HrUserId = hrUserId,
            CandidateUserId = recommendation.CandidateUserId,
            Message = string.IsNullOrWhiteSpace(dto.Message) ? null : dto.Message.Trim()
        };
        await _invitationRepository.AddAsync(invitation);

        recommendation.Status = CandidateRecommendationStatus.Invited;
        recommendation.UpdatedAt = DateTime.UtcNow;
        await _recommendationRepository.UpdateAsync(recommendation);

        return new InviteCandidateResponseDto
        {
            RecommendationId = recommendation.Id,
            InvitationId = invitation.Id,
            RecommendationStatus = recommendation.Status,
            InvitationStatus = invitation.Status
        };
    }

    private async Task<CandidateRecommendation> GetOwnedRecommendationAsync(Guid id, Guid hrUserId)
    {
        var recommendation = await _recommendationRepository.GetByIdAsync(id)
            ?? throw new NotFoundException("Recommendation không tồn tại.");

        if (recommendation.HrOwnerId != hrUserId)
            throw new ForbiddenException("Bạn không có quyền thao tác trên recommendation này.");

        return recommendation;
    }
}

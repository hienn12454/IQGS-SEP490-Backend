using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.Hr;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class HrTalentService : IHrTalentService
{
    private readonly IPracticeSessionRepository _sessionRepository;

    public HrTalentService(IPracticeSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<PagedResultDto<HrTalentItemDto>> ListAsync(Guid hrUserId, HrTalentListQueryDto query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        if (query.MinScore is double min && (min < 0 || min > 100))
            throw new BadRequestException("MinScore phải nằm trong khoảng 0–100.");

        string? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
            status = query.Status.Trim().ToUpperInvariant();

        var (items, total) = await _sessionRepository.ListTalentForHrAsync(
            hrUserId, page, pageSize, query.Keyword, query.QuestionSetId, status, query.MinScore);

        return new PagedResultDto<HrTalentItemDto>
        {
            Items = items.Select(r => new HrTalentItemDto
            {
                SessionId = r.SessionId,
                CandidateUserId = r.CandidateUserId,
                CandidateName = r.CandidateName,
                CandidateEmail = r.CandidateEmail,
                TargetRole = r.TargetRole,
                SeniorityLevel = r.SeniorityLevel,
                QuestionSetId = r.QuestionSetId,
                QuestionSetTitle = r.QuestionSetTitle,
                SessionStatus = r.SessionStatus,
                OverallScore = r.OverallScore,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                RecommendationId = r.RecommendationId,
                RecommendationStatus = r.RecommendationStatus,
                InvitationStatus = r.InvitationStatus
            }).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
